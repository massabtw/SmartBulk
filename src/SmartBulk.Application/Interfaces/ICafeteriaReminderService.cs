using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface ICafeteriaReminderService
{
    Task CheckAndSendRemindersAsync(DateTimeOffset currentTimeUtc, CancellationToken cancellationToken = default);
    Task<Result> ProcessPlateProfileCallbackAsync(long chatId, string callbackData, CancellationToken cancellationToken = default);
}

