namespace SmartBulk.Application.Models;

public class ShakeAdjustedIngredientModel
{
    public Guid FoodUuid { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal OriginalGrams { get; set; }
    public decimal ScaledGrams { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
}

