namespace SmartBulk.Domain.Nutrition;

public static class NutritionCalculator
{
    public static decimal CalculateProteinTarget(decimal proteinTargetGPerKg, decimal? latestWeightKg)
    {
        if (!latestWeightKg.HasValue || latestWeightKg.Value <= 0 || proteinTargetGPerKg <= 0)
            return 0m;

        return Math.Round(proteinTargetGPerKg * latestWeightKg.Value, 1);
    }

    public static DailyGap CalculateDailyGap(
        decimal calorieTarget,
        decimal proteinTarget,
        NutritionalValue? consumed,
        NutritionalValue? guaranteedCafeteria)
    {
        var consumedSafe = consumed ?? NutritionalValue.Zero;
        var cafeteriaSafe = guaranteedCafeteria ?? NutritionalValue.Zero;

        var totalKcal = consumedSafe.Calories + cafeteriaSafe.Calories;
        var totalProtein = consumedSafe.Protein + cafeteriaSafe.Protein;

        var calorieGap = Math.Max(0m, calorieTarget - totalKcal);
        var proteinGap = Math.Max(0m, proteinTarget - totalProtein);

        return new DailyGap
        {
            CalorieTarget = calorieTarget,
            ProteinTarget = proteinTarget,
            Consumed = consumedSafe,
            GuaranteedCafeteria = cafeteriaSafe,
            CalorieGap = Math.Round(calorieGap, 1),
            ProteinGap = Math.Round(proteinGap, 1)
        };
    }

    public static NutritionalValue ApplyPlateProfile(NutritionalValue plateNutrition, PlateProfile profile)
    {
        if (plateNutrition is null)
            return NutritionalValue.Zero;

        return plateNutrition.Multiply(profile.GetMultiplier());
    }

    public static decimal ConvertReadyToRaw(decimal readyGrams, decimal? rawYieldFactor)
    {
        if (readyGrams <= 0)
            return 0m;

        if (!rawYieldFactor.HasValue || rawYieldFactor.Value <= 0)
            return readyGrams;

        return Math.Round(readyGrams / rawYieldFactor.Value, 1);
    }
}
