namespace SmartBulk.Domain.Nutrition;

public class DailyGap
{
    public decimal CalorieTarget { get; init; }
    public decimal ProteinTarget { get; init; }
    public NutritionalValue Consumed { get; init; } = NutritionalValue.Zero;
    public NutritionalValue GuaranteedCafeteria { get; init; } = NutritionalValue.Zero;
    public decimal CalorieGap { get; init; }
    public decimal ProteinGap { get; init; }

    public decimal TotalExpectedCalories => Consumed.Calories + GuaranteedCafeteria.Calories;
    public decimal TotalExpectedProtein => Consumed.Protein + GuaranteedCafeteria.Protein;

    public bool HasReachedCalorieTarget => CalorieGap == 0;
    public bool HasReachedProteinTarget => ProteinGap == 0;
}
