namespace SmartBulk.Application.Models;

public class ShakeExtraItemModel
{
    public Guid FoodUuid { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal Grams { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public string Reason { get; set; } = string.Empty;
}

