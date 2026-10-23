namespace SmartBulk.Application.Models;

public class MealLogItemModel
{
    public Guid Uuid { get; set; }
    public Guid FoodUuid { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal ServingGrams { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
}

public class MealLogModel
{
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public List<MealLogItemModel> Items { get; set; } = new();
}
