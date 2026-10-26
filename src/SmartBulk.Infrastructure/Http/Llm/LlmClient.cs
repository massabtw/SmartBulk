using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                var mimeType = !string.IsNullOrWhiteSpace(imageMimeType) ? imageMimeType.Trim() : "image/jpeg";
                var cleanBase64 = imageBase64.Trim();

                // If input already has a data URL prefix (e.g. data:image/png;base64,ABC...), strip it and extract mimeType
                if (cleanBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var commaIdx = cleanBase64.IndexOf(',');
                    if (commaIdx >= 0)
                    {
                        var prefix = cleanBase64.Substring(0, commaIdx);
                        cleanBase64 = cleanBase64.Substring(commaIdx + 1).Trim();

                        if (string.IsNullOrWhiteSpace(imageMimeType))
                        {
                            var semiIdx = prefix.IndexOf(';');
                            if (semiIdx > 5)
                            {
                                mimeType = prefix.Substring(5, semiIdx - 5).Trim();
                            }
                        }
                    }
                }

                cleanBase64 = cleanBase64.Replace("\r", "").Replace("\n", "").Replace(" ", "");

                var dataUri = $"data:{mimeType};base64,{cleanBase64}";
                var parts = new List<LlmChatContentPartModel>
                {
                    new() { Type = "text", Text = prompt },
                    new()
                    {
                        Type = "image_url",
                        ImageUrl = new LlmChatImageUrlModel
                        {
                            Url = dataUri
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

                var (visionResult, _, visionStatusCode, isVisionTimeout) = await ExecuteProviderCallAsync(visionProvider, request, cancellationToken);
                if (visionResult.Success)
                {
                    return visionResult;
                }

                var groqModel = await ResolveGroqVisionModelAsync(fastTextProvider, cancellationToken);
                var groqVisionProvider = GetEffectiveGroqVisionProvider(fastTextProvider, groqModel);
                if (CanFallback(visionProvider, groqVisionProvider))
                {
                    _logger.LogWarning(
                        "Chamada com imagem para VisionAndFallbackProvider ({Model}) falhou com status {StatusCode} (timeout: {IsTimeout}, erro: {Error}). Acionando fallback automático para Groq Vision ({GroqModel}) em {BaseUrl}.",
                        visionProvider.Model,
                        visionStatusCode,
                        isVisionTimeout,
                        visionResult.Errors.FirstOrDefault(),
                        groqVisionProvider.Model,
                        groqVisionProvider.BaseUrl);

                    var (groqResult, _, groqStatusCode, _) = await ExecuteProviderCallAsync(groqVisionProvider, request, cancellationToken);

                    // Se retornar 404 (model_not_found), força refresh da descoberta de modelos e tenta mais uma vez
                    if (!groqResult.Success && groqStatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Groq retornou 404 para o modelo {Model}. Forçando redescoberta via /models...", groqModel);
                        var refreshedModel = await ResolveGroqVisionModelAsync(fastTextProvider, cancellationToken, forceRefresh: true);
                        if (!string.Equals(refreshedModel, groqModel, StringComparison.OrdinalIgnoreCase))
                        {
                            var refreshedProvider = GetEffectiveGroqVisionProvider(fastTextProvider, refreshedModel);
                            var (retryGroqResult, _, _, _) = await ExecuteProviderCallAsync(refreshedProvider, request, cancellationToken);
                            if (retryGroqResult.Success)
                            {
                                _logger.LogInformation(
                                    "Fallback com imagem para Groq Vision redescoberto ({Model}) executado com sucesso.",
                                    refreshedModel);
                                return retryGroqResult;
                            }
                            return retryGroqResult;
                        }
                    }

                    if (groqResult.Success)
                    {
                        _logger.LogInformation(
                            "Fallback com imagem para Groq Vision ({Model}) executado com sucesso.",
                            groqVisionProvider.Model);
                        return groqResult;
                    }

                    _logger.LogError(
                        "Fallback com imagem para Groq Vision ({Model}) também falhou: {Error}",
                        groqVisionProvider.Model,
                        groqResult.Errors.FirstOrDefault());

                    return groqResult;
                }

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

        var timeoutSeconds = provider.TimeoutSeconds > 0 ? provider.TimeoutSeconds : 60;
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
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("LLM provider em {BaseUrl} retornou 429 TooManyRequests: {Body}", provider.BaseUrl, errorBody);
                return (
                    Result<LlmChatCompletionResponseModel>.Fail($"LLM HTTP 429 (TooManyRequests) em {provider.BaseUrl}: Limite de taxa ou cota excedida na LLM. Tente novamente mais tarde."),
                    true,
                    HttpStatusCode.TooManyRequests,
                    false);
            }

            if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("LLM provider em {BaseUrl} retornou erro de servidor: {StatusCode} {Body}", provider.BaseUrl, response.StatusCode, errorBody);
                return (
                    Result<LlmChatCompletionResponseModel>.Fail($"LLM HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) em {provider.BaseUrl}: {errorBody}"),
                    true,
                    response.StatusCode,
                    false);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LLM provider em {BaseUrl} falhou autenticação: {StatusCode} {Body}", provider.BaseUrl, response.StatusCode, errorBody);
                var detail = !string.IsNullOrWhiteSpace(errorBody) ? errorBody : "Chave de API inválida ou sem permissão.";
                return (
                    Result<LlmChatCompletionResponseModel>.Fail($"LLM HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) em {provider.BaseUrl}: {detail}"),
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

                var detail = parsedMessage ?? errorBody;
                var errorMsg = $"LLM HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) em {provider.BaseUrl}: {detail}".Trim();
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
                Result<LlmChatCompletionResponseModel>.Fail($"LLM Timeout: Tempo limite de {timeoutSeconds}s esgotado ao comunicar com {provider.BaseUrl}."),
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
            var statusPrefix = ex.StatusCode.HasValue ? $"LLM HTTP {(int)ex.StatusCode.Value} ({ex.StatusCode.Value})" : "Erro de conexão LLM";
            return (
                Result<LlmChatCompletionResponseModel>.Fail($"{statusPrefix} ao comunicar com {provider.BaseUrl}: {ex.Message}"),
                true,
                ex.StatusCode,
                false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao comunicar com LLM provider em {BaseUrl}.", provider.BaseUrl);
            return (
                Result<LlmChatCompletionResponseModel>.Fail($"Erro inesperado na LLM ({provider.BaseUrl}): {ex.Message}"),
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
                TimeoutSeconds = _options.FastTextProvider.TimeoutSeconds > 0 ? _options.FastTextProvider.TimeoutSeconds : 60
            };
        }

        return new LlmProviderOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl) ? _options.BaseUrl : "https://api.groq.com/openai/v1/",
            ApiKey = _options.ApiKey,
            Model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "llama-3.3-70b-versatile",
            TimeoutSeconds = 60
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
                    : (!string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "openai/gpt-4o"),
                TimeoutSeconds = _options.VisionAndFallbackProvider.TimeoutSeconds > 0 ? _options.VisionAndFallbackProvider.TimeoutSeconds : 60
            };
        }

        return new LlmProviderOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl) ? _options.BaseUrl : "https://models.github.ai/inference/",
            ApiKey = _options.ApiKey,
            Model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "openai/gpt-4o",
            TimeoutSeconds = 60
        };
    }

    private static LlmProviderOptions GetEffectiveGroqVisionProvider(LlmProviderOptions fastTextProvider, string? model = null)
    {
        var groqVisionModel = !string.IsNullOrWhiteSpace(model) ? model : "qwen/qwen3.8-27b";
        return new LlmProviderOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(fastTextProvider.BaseUrl)
                ? fastTextProvider.BaseUrl
                : "https://api.groq.com/openai/v1/",
            ApiKey = fastTextProvider.ApiKey,
            Model = groqVisionModel,
            TimeoutSeconds = fastTextProvider.TimeoutSeconds > 0 ? fastTextProvider.TimeoutSeconds : 60
        };
    }

    private static string? _cachedGroqVisionModel;
    private static readonly SemaphoreSlim _groqDiscoveryLock = new(1, 1);

    private async Task<string> ResolveGroqVisionModelAsync(
        LlmProviderOptions fastTextProvider,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedGroqVisionModel))
        {
            return _cachedGroqVisionModel;
        }

        await _groqDiscoveryLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedGroqVisionModel))
            {
                return _cachedGroqVisionModel;
            }

            var discovered = await QueryGroqVisionModelFromApiAsync(fastTextProvider, cancellationToken);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                _cachedGroqVisionModel = discovered;
                return discovered;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na descoberta dinâmica de modelos na Groq (/models). Usando fallback padrão.");
        }
        finally
        {
            _groqDiscoveryLock.Release();
        }

        return "qwen/qwen3.8-27b";
    }

    private async Task<string?> QueryGroqVisionModelFromApiAsync(
        LlmProviderOptions fastTextProvider,
        CancellationToken cancellationToken)
    {
        var modelsEndpoint = ResolveModelsEndpoint(fastTextProvider.BaseUrl);
        var request = new HttpRequestMessage(HttpMethod.Get, modelsEndpoint);
        if (!string.IsNullOrWhiteSpace(fastTextProvider.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fastTextProvider.ApiKey.Trim());
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        using var response = await _httpClient.SendAsync(request, linkedCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Consulta a {Endpoint} retornou status HTTP {StatusCode}", modelsEndpoint, response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(linkedCts.Token);
        var list = JsonSerializer.Deserialize<GroqModelsListResponse>(json, JsonOptions);
        if (list?.Data == null || list.Data.Count == 0)
        {
            return null;
        }

        // 1. Busca modelos ativos cuja lista de 'input_modalities' inclua 'image'
        var visionByModality = list.Data
            .Where(m => m.Active && !string.IsNullOrWhiteSpace(m.Id))
            .FirstOrDefault(m => m.InputModalities != null &&
                                 m.InputModalities.Any(mod => string.Equals(mod, "image", StringComparison.OrdinalIgnoreCase)));

        if (visionByModality != null && !string.IsNullOrWhiteSpace(visionByModality.Id))
        {
            _logger.LogInformation("Modelo de visão ativo descoberto na Groq por modalidade 'image': {Model}", visionByModality.Id);
            return visionByModality.Id;
        }

        // 2. Busca por modelos ativos que contenham identificadores de visão conhecidos
        var knownVisionKeywords = new[] { "vision", "qwen", "scout", "maverick" };
        var visionByKeyword = list.Data
            .Where(m => m.Active && !string.IsNullOrWhiteSpace(m.Id))
            .FirstOrDefault(m => knownVisionKeywords.Any(k => m.Id!.Contains(k, StringComparison.OrdinalIgnoreCase)));

        if (visionByKeyword != null && !string.IsNullOrWhiteSpace(visionByKeyword.Id))
        {
            _logger.LogInformation("Modelo de visão ativo descoberto na Groq por identificador: {Model}", visionByKeyword.Id);
            return visionByKeyword.Id;
        }

        return null;
    }

    private static string ResolveModelsEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "https://api.groq.com/openai/v1/models";

        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - "/chat/completions".Length);
        }

        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"{trimmed}/models";
    }

    private class GroqModelsListResponse
    {
        [JsonPropertyName("data")]
        public List<GroqModelItemModel>? Data { get; set; }
    }

    private class GroqModelItemModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("input_modalities")]
        public List<string>? InputModalities { get; set; }
    }

    private static LlmChatCompletionRequestModel PrepareRequestForProvider(
        LlmChatCompletionRequestModel original,
        LlmProviderOptions provider)
    {
        var model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : original.Model;

        if (!string.IsNullOrWhiteSpace(provider.BaseUrl) &&
            provider.BaseUrl.Contains("models.github.ai", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(model) &&
            !model.Contains('/'))
        {
            model = $"openai/{model}";
        }

        return new LlmChatCompletionRequestModel
        {
            Model = model,
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
