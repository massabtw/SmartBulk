namespace SmartBulk.Domain.Entities;

public class MealLogEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<MealLogItemEntity> Items { get; set; } = new();
}
