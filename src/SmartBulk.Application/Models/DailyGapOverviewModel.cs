namespace SmartBulk.Application.Models;

public class DailyGapOverviewModel
{
    public DateOnly Date { get; set; }
    public int CalorieTarget { get; set; }
    public decimal ProteinTarget { get; set; }
    public decimal? LatestWeightKg { get; set; }
    public string? WeightWarning { get; set; }

    public decimal ConsumedCalories { get; set; }
    public decimal ConsumedProtein { get; set; }
    public decimal ConsumedCarbohydrates { get; set; }
    public decimal ConsumedFat { get; set; }

    public decimal CafeteriaCalories { get; set; }
    public decimal CafeteriaProtein { get; set; }
    public decimal CafeteriaCarbohydrates { get; set; }
    public decimal CafeteriaFat { get; set; }

    public decimal TotalExpectedCalories => ConsumedCalories + CafeteriaCalories;
    public decimal TotalExpectedProtein => ConsumedProtein + CafeteriaProtein;

    public decimal CalorieGap { get; set; }
    public decimal ProteinGap { get; set; }

    public bool HasReachedCalorieTarget => CalorieGap == 0;
    public bool HasReachedProteinTarget => ProteinGap == 0;

    public List<MealLogModel> Meals { get; set; } = new();
    public CafeteriaDailyGuaranteedModel? Cafeteria { get; set; }
}
