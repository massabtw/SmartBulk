namespace SmartBulk.Domain.Entities;

public class HomeMealPlanItemEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int HomeMealPlanId { get; set; }
    public DateOnly Date { get; set; }
    public string MealSlot { get; set; } = string.Empty; // "Breakfast", "Lunch", "Dinner", "Shake"
    public int MealTemplateId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public MealTemplateEntity? MealTemplate { get; set; }
}

