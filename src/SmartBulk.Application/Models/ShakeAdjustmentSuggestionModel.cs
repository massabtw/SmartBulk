namespace SmartBulk.Application.Models;

public class ShakeAdjustmentSuggestionModel
{
    public bool NoAdjustmentNeeded { get; set; }
    public decimal ScaleFactor { get; set; }
    public decimal BaseCalories { get; set; }
    public decimal BaseProtein { get; set; }
    public decimal ScaledCalories { get; set; }
    public decimal ScaledProtein { get; set; }
    public List<ShakeAdjustedIngredientModel> Ingredients { get; set; } = new();
    public ShakeExtraItemModel? ExtraItem { get; set; }
    public decimal ResultingCalorieGap { get; set; }
    public decimal ResultingProteinGap { get; set; }
}

