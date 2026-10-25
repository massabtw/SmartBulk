using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Domain.Http.Telegram;

public interface ITelegramClient
{
    Task<Result> SendMessageAsync(
        long chatId,
        string text,
        TelegramInlineKeyboardMarkupModel? replyMarkup = null,
        string? parseMode = null,
        CancellationToken cancellationToken = default);

    Task<Result> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default);

    Task<Result<List<TelegramUpdateModel>>> GetUpdatesAsync(
        long? offset = null,
        int limit = 100,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default);

    Task<Result<TelegramFileModel>> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    Task<Result<string>> DownloadFileAsBase64Async(
        string filePath,
        CancellationToken cancellationToken = default);
}

