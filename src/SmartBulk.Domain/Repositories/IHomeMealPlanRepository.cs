using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IHomeMealPlanRepository
{
    Task<HomeMealPlanEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default);
    Task<HomeMealPlanEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<HomeMealPlanEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(HomeMealPlanEntity entity, CancellationToken cancellationToken = default);
    Task<int> CreateItemAsync(HomeMealPlanItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(HomeMealPlanEntity entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task ReplaceItemsAsync(int planId, IEnumerable<HomeMealPlanItemEntity> items, CancellationToken cancellationToken = default);
}

