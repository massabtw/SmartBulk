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

