using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IMealLogRepository
{
    Task<MealLogEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<MealLogEntity?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MealLogEntity>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<int> CreateMealAsync(MealLogEntity meal, CancellationToken cancellationToken = default);
    Task<int> CreateMealItemAsync(MealLogItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> DeleteMealAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<bool> DeleteMealItemAsync(Guid itemUuid, CancellationToken cancellationToken = default);
    Task<bool> DeleteLatestMealAsync(CancellationToken cancellationToken = default);
}
