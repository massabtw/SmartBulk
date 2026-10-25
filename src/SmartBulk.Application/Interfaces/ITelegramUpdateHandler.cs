using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface ITelegramUpdateHandler
{
    Task<Result> HandleUpdateAsync(TelegramUpdateModel update, CancellationToken cancellationToken = default);
}

