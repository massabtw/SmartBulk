using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IMealLogService
{
    Task<Result<MealLogModel>> CreateMealLogAsync(MealLogCreateModel model, CancellationToken cancellationToken = default);
    Task<Result<MealLogModel>> GetMealLogByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<MealLogModel>>> GetMealLogsByDateAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<Result> DeleteMealLogAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result> DeleteMealLogItemAsync(Guid itemUuid, CancellationToken cancellationToken = default);
    Task<Result> DeleteLatestMealLogAsync(CancellationToken cancellationToken = default);
    Task<Result<DailyGapOverviewModel>> GetDailyGapOverviewAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
}
