namespace SmartBulk.Application.Models;

public class ShoppingListItemModel
{
    public Guid Uuid { get; set; }
    public Guid FoodUuid { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal PlannedGrams { get; set; }
    public decimal RawGrams { get; set; }
    public decimal? RawYieldFactor { get; set; }
    public bool IsPurchased { get; set; }
}

