namespace SmartBulk.Domain.Entities;

public class MealTemplateItemEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int MealTemplateId { get; set; }
    public int FoodId { get; set; }
    public decimal ServingGrams { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public FoodEntity? Food { get; set; }
}

