namespace SmartBulk.Domain.Entities;

public class ShoppingListEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<ShoppingListItemEntity> Items { get; set; } = new();
}

