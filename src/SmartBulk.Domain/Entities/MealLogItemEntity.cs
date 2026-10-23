namespace SmartBulk.Domain.Entities;

public class MealLogItemEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int MealLogId { get; set; }
    public int FoodId { get; set; }
    public decimal ServingGrams { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public FoodEntity? Food { get; set; }
}
