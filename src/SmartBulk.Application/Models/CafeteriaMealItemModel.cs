namespace SmartBulk.Application.Models;

public class CafeteriaMealItemModel
{
    public Guid Uuid { get; set; }
    public Guid? FoodUuid { get; set; }
    public string RawName { get; set; } = string.Empty;
    public string? MatchedFoodName { get; set; }
    public decimal ServingGrams { get; set; }
    public bool IsPending { get; set; }

    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
}
