using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface ICafeteriaMenuRepository
{
    Task<CafeteriaMenuEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default);
    Task<CafeteriaMenuEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<CafeteriaMealEntity?> GetMealByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<CafeteriaMealItemEntity?> GetItemByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<int> CreateMenuAsync(CafeteriaMenuEntity menu, CancellationToken cancellationToken = default);
    Task<int> CreateMealAsync(CafeteriaMealEntity meal, CancellationToken cancellationToken = default);
    Task<int> CreateMealItemAsync(CafeteriaMealItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(int menuId, string status, CancellationToken cancellationToken = default);
    Task<bool> UpdateMealPlateProfileAsync(int mealId, string plateProfile, CancellationToken cancellationToken = default);
    Task<bool> UpdateMealItemAsync(CafeteriaMealItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> DeleteMealItemAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<bool> DeleteMenuAsync(Guid uuid, CancellationToken cancellationToken = default);
}
