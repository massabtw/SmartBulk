using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IShoppingListRepository
{
    Task<ShoppingListEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default);
    Task<ShoppingListEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<ShoppingListItemEntity?> GetItemByUuidAsync(Guid itemUuid, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(ShoppingListEntity entity, CancellationToken cancellationToken = default);
    Task<int> CreateItemAsync(ShoppingListItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> UpdateItemAsync(ShoppingListItemEntity item, CancellationToken cancellationToken = default);
    Task<bool> ToggleItemPurchasedAsync(Guid itemUuid, bool isPurchased, CancellationToken cancellationToken = default);
    Task SyncItemsAsync(int shoppingListId, IEnumerable<ShoppingListItemEntity> items, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
}

