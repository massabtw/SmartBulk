namespace SmartBulk.Application.Models;

public class ShoppingListModel
{
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public int TotalItems { get; set; }
    public int PurchasedItems { get; set; }
    public bool IsAllPurchased { get; set; }
    public decimal TotalPlannedGrams { get; set; }
    public decimal TotalRawGrams { get; set; }
    public List<ShoppingListItemModel> Items { get; set; } = new();
}

