using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IMealTemplateRepository
{
    Task<MealTemplateEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<MealTemplateEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MealTemplateEntity>> GetAllAsync(string? mealType = null, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(MealTemplateEntity entity, CancellationToken cancellationToken = default);
    Task<int> CreateItemAsync(MealTemplateItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(MealTemplateEntity entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task ReplaceItemsAsync(int templateId, IEnumerable<MealTemplateItemEntity> items, CancellationToken cancellationToken = default);
}

