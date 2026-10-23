using System.Net;
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
                Model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "gpt-4o",
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
            var endpoint = ResolveCompletionsEndpoint(_options.BaseUrl);

            using var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("LLM rate limit or quota exceeded (HTTP 429).");
                return Result<LlmChatCompletionResponseModel>.Fail("Limite de taxa ou cota excedida na LLM (HTTP 429). Tente novamente mais tarde.");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogError("LLM authentication failed with status: {StatusCode}", response.StatusCode);
                return Result<LlmChatCompletionResponseModel>.Fail("Falha de autenticação com a API de LLM.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LLM request failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LlmChatErrorResponseModel>(errorBody, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(errorResponse?.Error?.Message))
                    {
                        return Result<LlmChatCompletionResponseModel>.Fail(errorResponse.Error.Message);
                    }
                }
                catch
                {
                    // Body is not standard json error
                }

                return Result<LlmChatCompletionResponseModel>.Fail($"Erro na chamada da LLM: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var completion = await response.Content.ReadFromJsonAsync<LlmChatCompletionResponseModel>(JsonOptions, cancellationToken);
            if (completion is null)
            {
                return Result<LlmChatCompletionResponseModel>.Fail("Falha ao desserializar resposta da LLM.");
            }

            return completion;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with LLM.");
            return Result<LlmChatCompletionResponseModel>.Fail("Erro de conexão ao comunicar com o serviço de LLM.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request timed out or was canceled.");
            return Result<LlmChatCompletionResponseModel>.Fail("Tempo limite esgotado ao comunicar com o serviço de LLM.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in LLM SendChatAsync.");
            return ErrorMessages.UnknownError;
        }
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
