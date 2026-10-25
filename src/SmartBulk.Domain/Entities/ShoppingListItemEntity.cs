namespace SmartBulk.Domain.Entities;

public class ShoppingListItemEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int ShoppingListId { get; set; }
    public int FoodId { get; set; }
    public decimal PlannedGrams { get; set; }
    public decimal RawGrams { get; set; }
    public bool IsPurchased { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public FoodEntity? Food { get; set; }
}

