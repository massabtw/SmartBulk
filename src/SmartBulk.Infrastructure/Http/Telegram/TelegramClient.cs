using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Infrastructure.Http.Telegram;

public class TelegramClient : ITelegramClient
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public TelegramClient(
        HttpClient httpClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendMessageAsync(
        long chatId,
        string text,
        TelegramInlineKeyboardMarkupModel? replyMarkup = null,
        string? parseMode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Telegram BotToken not configured. Message not sent.");
            return Result.Ok();
        }

        try
        {
            var url = BuildBotUrl("sendMessage");
            var request = new TelegramSendMessageRequestModel
            {
                ChatId = chatId,
                Text = text,
                ParseMode = parseMode,
                ReplyMarkup = replyMarkup
            };

            using var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Telegram message: {StatusCode} {Error}", response.StatusCode, error);
                return Result.Fail($"Erro ao enviar mensagem no Telegram: {response.StatusCode}");
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram message to chatId {ChatId}", chatId);
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return Result.Ok();
        }

        try
        {
            var url = BuildBotUrl("answerCallbackQuery");
            var request = new TelegramAnswerCallbackQueryRequestModel
            {
                CallbackQueryId = callbackQueryId,
                Text = text
            };

            using var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering Telegram callback query {Id}", callbackQueryId);
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<List<TelegramUpdateModel>>> GetUpdatesAsync(
        long? offset = null,
        int limit = 100,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return new List<TelegramUpdateModel>();
        }

        try
        {
            var url = BuildBotUrl("getUpdates");
            var payload = new
            {
                offset,
                limit,
                timeout = timeoutSeconds
            };

            using var response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to get Telegram updates: {StatusCode} {Error}", response.StatusCode, error);
                return new List<TelegramUpdateModel>();
            }

            var content = await response.Content.ReadFromJsonAsync<TelegramUpdatesResponseModel>(JsonOptions, cancellationToken);
            return content?.Result ?? new List<TelegramUpdateModel>();
        }
        catch (OperationCanceledException)
        {
            return new List<TelegramUpdateModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Telegram updates");
            return Result<List<TelegramUpdateModel>>.Fail(ex.Message);
        }
    }

    public async Task<Result<TelegramFileModel>> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return Result<TelegramFileModel>.Fail("BotToken não configurado.");
        }

        try
        {
            var url = BuildBotUrl($"getFile?file_id={Uri.EscapeDataString(fileId)}");
            var response = await _httpClient.GetFromJsonAsync<TelegramFileResponseModel>(url, JsonOptions, cancellationToken);
            if (response is null || !response.Ok || response.Result is null)
            {
                return Result<TelegramFileModel>.Fail("Não foi possível obter dados do arquivo do Telegram.");
            }

            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Telegram file info for fileId: {FileId}", fileId);
            return Result<TelegramFileModel>.Fail(ex.Message);
        }
    }

    public async Task<Result<string>> DownloadFileAsBase64Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return Result<string>.Fail("BotToken não configurado.");
        }

        try
        {
            var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "https://api.telegram.org";
            var fileUrl = $"{baseUrl}/file/bot{_options.BotToken}/{filePath}";

            var bytes = await _httpClient.GetByteArrayAsync(fileUrl, cancellationToken);
            var base64 = Convert.ToBase64String(bytes);
            return Result<string>.Ok(base64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading Telegram file from path: {FilePath}", filePath);
            return Result<string>.Fail(ex.Message);
        }
    }

    private string BuildBotUrl(string method)
    {
        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "https://api.telegram.org";
        return $"{baseUrl}/bot{_options.BotToken}/{method}";
    }

    private class TelegramUpdatesResponseModel
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public List<TelegramUpdateModel> Result { get; set; } = new();
    }
}

