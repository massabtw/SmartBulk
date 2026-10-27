using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IShoppingListService
{
    Task<Result<ShoppingListModel?>> GetByWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default);
    Task<Result<ShoppingListModel>> GenerateFromPlanAsync(DateOnly? weekStartDate = null, bool forceRegeneratePlan = false, CancellationToken cancellationToken = default);
    Task<Result<ShoppingListItemModel>> TogglePurchasedAsync(Guid itemUuid, bool isPurchased, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
}

