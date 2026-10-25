using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Infrastructure.Http.Llm;

public class LlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<LlmClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<LlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateCompletionAsync(
        string prompt,
        string? systemMessage = null,
        bool jsonMode = false,
        string? imageBase64 = null,
        string? imageMimeType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LlmChatCompletionRequestModel
            {
                Messages = new List<LlmChatMessageModel>()
            };

            if (!string.IsNullOrWhiteSpace(systemMessage))
            {
                request.Messages.Add(new LlmChatMessageModel
                {
                    Role = "system",
                    Content = systemMessage
                });
            }

            if (!string.IsNullOrWhiteSpace(imageBase64))
            {
                var mimeType = !string.IsNullOrWhiteSpace(imageMimeType) ? imageMimeType : "image/jpeg";
                var parts = new List<LlmChatContentPartModel>
                {
                    new() { Type = "text", Text = prompt },
                    new()
                    {
                        Type = "image_url",
                        ImageUrl = new LlmChatImageUrlModel
                        {
                            Url = $"data:{mimeType};base64,{imageBase64}"
                        }
                    }
                };

                request.Messages.Add(new LlmChatMessageModel
                {
                    Role = "user",
                    Content = parts
                });
            }
            else
            {
                request.Messages.Add(new LlmChatMessageModel
                {
                    Role = "user",
                    Content = prompt
                });
            }

            if (jsonMode)
            {
                request.ResponseFormat = new LlmChatResponseFormatModel
                {
                    Type = "json_object"
                };
            }

            var sendResult = await SendChatAsync(request, cancellationToken);
            if (!sendResult.Success || sendResult.Data is null)
            {
                return Result<string>.Fail(sendResult.Errors);
            }

            var choice = sendResult.Data.Choices.FirstOrDefault();
            var text = choice?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<string>.Fail("A LLM retornou uma resposta vazia.");
            }

            return Result<string>.Ok(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating LLM completion.");
            return Result<string>.Fail(ErrorMessages.UnknownError);
        }
    }

    public async Task<Result<LlmChatCompletionResponseModel>> SendChatAsync(
        LlmChatCompletionRequestModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fastTextProvider = GetEffectiveFastTextProvider();
            var visionProvider = GetEffectiveVisionAndFallbackProvider();

            var hasImage = HasImage(request);

            if (hasImage)
            {
                _logger.LogInformation(
                    "Roteando chamada LLM com imagem diretamente para VisionAndFallbackProvider ({Model}) em {BaseUrl}.",
                    visionProvider.Model,
                    visionProvider.BaseUrl);

                var (visionResult, _, _, _) = await ExecuteProviderCallAsync(visionProvider, request, cancellationToken);
                return visionResult;
            }

            _logger.LogInformation(
                "Roteando chamada LLM de texto para FastTextProvider ({Model}) em {BaseUrl}.",
                fastTextProvider.Model,
                fastTextProvider.BaseUrl);

            var (primaryResult, isFallbackEligible, statusCode, isTimeout) =
                await ExecuteProviderCallAsync(fastTextProvider, request, cancellationToken);

            if (primaryResult.Success)
            {
                return primaryResult;
            }

            if (isFallbackEligible && CanFallback(fastTextProvider, visionProvider))
            {
                _logger.LogWarning(
                    "FastTextProvider falhou com status {StatusCode} (timeout: {IsTimeout}, erro: {Error}). Acionando fallback transparente para VisionAndFallbackProvider ({Model}) em {BaseUrl}.",
                    statusCode,
                    isTimeout,
                    primaryResult.Errors.FirstOrDefault(),
                    visionProvider.Model,
                    visionProvider.BaseUrl);

                var (fallbackResult, _, _, _) = await ExecuteProviderCallAsync(visionProvider, request, cancellationToken);
                if (fallbackResult.Success)
                {
                    _logger.LogInformation("Fallback para VisionAndFallbackProvider executado com sucesso.");
                    return fallbackResult;
                }

                _logger.LogError(
                    "Fallback para VisionAndFallbackProvider também falhou: {Error}",
                    fallbackResult.Errors.FirstOrDefault());

                return fallbackResult;
            }

            return primaryResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in LLM SendChatAsync.");
            return Result<LlmChatCompletionResponseModel>.Fail(ErrorMessages.UnknownError);
        }
    }

    private async Task<(Result<LlmChatCompletionResponseModel> Result, bool IsFallbackEligible, HttpStatusCode? StatusCode, bool IsTimeout)> ExecuteProviderCallAsync(
        LlmProviderOptions provider,
        LlmChatCompletionRequestModel originalRequest,
        CancellationToken cancellationToken)
    {
        var request = PrepareRequestForProvider(originalRequest, provider);
        var endpoint = ResolveCompletionsEndpoint(provider.BaseUrl);

        var timeoutSeconds = provider.TimeoutSeconds > 0 ? provider.TimeoutSeconds : 30;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            }

            using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("LLM provider em {BaseUrl} retornou 429 TooManyRequests.", provider.BaseUrl);
                return (
                    Result<LlmChatCompletionResponseModel>.Fail("Limite de taxa ou cota excedida na LLM (HTTP 429). Tente novamente mais tarde."),
                    true,
                    HttpStatusCode.TooManyRequests,
                    false);
            }

            if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
            {
                _logger.LogWarning("LLM provider em {BaseUrl} retornou erro de servidor: {StatusCode}.", provider.BaseUrl, response.StatusCode);
                return (
                    Result<LlmChatCompletionResponseModel>.Fail($"Erro no servidor da LLM: {(int)response.StatusCode} {response.ReasonPhrase}"),
                    true,
                    response.StatusCode,
                    false);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogError("LLM provider em {BaseUrl} falhou autenticação: {StatusCode}", provider.BaseUrl, response.StatusCode);
                return (
                    Result<LlmChatCompletionResponseModel>.Fail("Falha de autenticação com a API de LLM."),
                    false,
                    response.StatusCode,
                    false);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LLM provider em {BaseUrl} falhou com status {StatusCode}: {ErrorBody}", provider.BaseUrl, response.StatusCode, errorBody);

                string? parsedMessage = null;
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LlmChatErrorResponseModel>(errorBody, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(errorResponse?.Error?.Message))
                    {
                        parsedMessage = errorResponse.Error.Message;
                    }
                }
                catch
                {
                    // Non-json or non-standard error body
                }

                var errorMsg = parsedMessage ?? $"Erro na chamada da LLM: {(int)response.StatusCode} {response.ReasonPhrase}";
                return (
                    Result<LlmChatCompletionResponseModel>.Fail(errorMsg),
                    false,
                    response.StatusCode,
                    false);
            }

            var completion = await response.Content.ReadFromJsonAsync<LlmChatCompletionResponseModel>(JsonOptions, cancellationToken);
            if (completion is null)
            {
                return (
                    Result<LlmChatCompletionResponseModel>.Fail("Falha ao desserializar resposta da LLM."),
                    false,
                    response.StatusCode,
                    false);
            }

            return (
                Result<LlmChatCompletionResponseModel>.Ok(completion),
                false,
                response.StatusCode,
                false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LLM provider em {BaseUrl} sofreu timeout após {Timeout}s.", provider.BaseUrl, timeoutSeconds);
            return (
                Result<LlmChatCompletionResponseModel>.Fail("Tempo limite esgotado ao comunicar com o serviço de LLM."),
                true,
                null,
                true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Requisição à LLM cancelada pelo chamador.");
            return (
                Result<LlmChatCompletionResponseModel>.Fail("Operação cancelada."),
                false,
                null,
                false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao comunicar com LLM provider em {BaseUrl}.", provider.BaseUrl);
            return (
                Result<LlmChatCompletionResponseModel>.Fail("Erro de conexão ao comunicar com o serviço de LLM."),
                true,
                null,
                false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao comunicar com LLM provider em {BaseUrl}.", provider.BaseUrl);
            return (
                Result<LlmChatCompletionResponseModel>.Fail(ErrorMessages.UnknownError),
                false,
                null,
                false);
        }
    }

    private LlmProviderOptions GetEffectiveFastTextProvider()
    {
        if (_options.FastTextProvider != null && !string.IsNullOrWhiteSpace(_options.FastTextProvider.BaseUrl))
        {
            return new LlmProviderOptions
            {
                BaseUrl = _options.FastTextProvider.BaseUrl,
                ApiKey = !string.IsNullOrWhiteSpace(_options.FastTextProvider.ApiKey) ? _options.FastTextProvider.ApiKey : _options.ApiKey,
                Model = !string.IsNullOrWhiteSpace(_options.FastTextProvider.Model)
                    ? _options.FastTextProvider.Model
                    : (!string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "llama-3.3-70b-versatile"),
                TimeoutSeconds = _options.FastTextProvider.TimeoutSeconds > 0 ? _options.FastTextProvider.TimeoutSeconds : 30
            };
        }

        return new LlmProviderOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl) ? _options.BaseUrl : "https://api.groq.com/openai/v1/",
            ApiKey = _options.ApiKey,
            Model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "llama-3.3-70b-versatile",
            TimeoutSeconds = 30
        };
    }

    private LlmProviderOptions GetEffectiveVisionAndFallbackProvider()
    {
        if (_options.VisionAndFallbackProvider != null && !string.IsNullOrWhiteSpace(_options.VisionAndFallbackProvider.BaseUrl))
        {
            return new LlmProviderOptions
            {
                BaseUrl = _options.VisionAndFallbackProvider.BaseUrl,
                ApiKey = !string.IsNullOrWhiteSpace(_options.VisionAndFallbackProvider.ApiKey) ? _options.VisionAndFallbackProvider.ApiKey : _options.ApiKey,
                Model = !string.IsNullOrWhiteSpace(_options.VisionAndFallbackProvider.Model)
                    ? _options.VisionAndFallbackProvider.Model
                    : (!string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "gpt-4o"),
                TimeoutSeconds = _options.VisionAndFallbackProvider.TimeoutSeconds > 0 ? _options.VisionAndFallbackProvider.TimeoutSeconds : 30
            };
        }

        return new LlmProviderOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl) ? _options.BaseUrl : "https://models.inference.ai.azure.com/",
            ApiKey = _options.ApiKey,
            Model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "gpt-4o",
            TimeoutSeconds = 30
        };
    }

    private static LlmChatCompletionRequestModel PrepareRequestForProvider(
        LlmChatCompletionRequestModel original,
        LlmProviderOptions provider)
    {
        return new LlmChatCompletionRequestModel
        {
            Model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : original.Model,
            Messages = original.Messages,
            ResponseFormat = original.ResponseFormat
        };
    }

    private static bool HasImage(LlmChatCompletionRequestModel? request)
    {
        if (request?.Messages == null)
            return false;

        foreach (var message in request.Messages)
        {
            if (message.Content is IEnumerable<LlmChatContentPartModel> parts)
            {
                if (parts.Any(p => string.Equals(p.Type, "image_url", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            else if (message.Content is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeProp) &&
                        string.Equals(typeProp.GetString(), "image_url", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool CanFallback(LlmProviderOptions primary, LlmProviderOptions fallback)
    {
        if (fallback is null || string.IsNullOrWhiteSpace(fallback.BaseUrl))
            return false;

        var sameBaseUrl = string.Equals(primary.BaseUrl?.TrimEnd('/'), fallback.BaseUrl?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        var sameModel = string.Equals(primary.Model, fallback.Model, StringComparison.OrdinalIgnoreCase);

        return !(sameBaseUrl && sameModel);
    }

    private static string ResolveCompletionsEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "chat/completions";

        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"{trimmed}/chat/completions";
    }
}
