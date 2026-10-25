using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IShakeAdjustmentService
{
    Task<Result<ShakeAdjustmentSuggestionModel>> GetShakeAdjustmentAsync(
        DateOnly? date = null,
        Guid? templateUuid = null,
        CancellationToken cancellationToken = default);
}

