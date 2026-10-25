using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class ShoppingListModelMap
{
    public static ShoppingListItemModel ToItemModel(this ShoppingListItemEntity entity)
    {
        return new ShoppingListItemModel
        {
            Uuid = entity.Uuid,
            FoodUuid = entity.Food?.Uuid ?? Guid.Empty,
            FoodName = entity.Food?.Name ?? string.Empty,
            PlannedGrams = Math.Round(entity.PlannedGrams, 2),
            RawGrams = Math.Round(entity.RawGrams, 2),
            RawYieldFactor = entity.Food?.RawYieldFactor,
            IsPurchased = entity.IsPurchased
        };
    }

    public static ShoppingListModel ToModel(this ShoppingListEntity entity)
    {
        var items = entity.Items?.Select(ToItemModel).ToList() ?? new List<ShoppingListItemModel>();
        var totalItems = items.Count;
        var purchasedItems = items.Count(i => i.IsPurchased);
        var totalPlanned = items.Sum(i => i.PlannedGrams);
        var totalRaw = items.Sum(i => i.RawGrams);

        return new ShoppingListModel
        {
            Uuid = entity.Uuid,
            WeekStartDate = entity.WeekStartDate,
            TotalItems = totalItems,
            PurchasedItems = purchasedItems,
            IsAllPurchased = totalItems > 0 && purchasedItems == totalItems,
            TotalPlannedGrams = Math.Round(totalPlanned, 2),
            TotalRawGrams = Math.Round(totalRaw, 2),
            Items = items
        };
    }
}

