using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IFoodRepository
{
    Task<FoodEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FoodEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<IEnumerable<FoodEntity>> SearchAsync(string? name, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? name, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(FoodEntity entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(FoodEntity entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
}
