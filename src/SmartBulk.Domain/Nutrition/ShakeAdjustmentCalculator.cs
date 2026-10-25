using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Nutrition;

public record ShakeAdjustedIngredient(
    Guid FoodUuid,
    string FoodName,
    decimal OriginalGrams,
    decimal ScaledGrams,
    NutritionalValue ScaledNutrition);

public record ShakeExtraItem(
    Guid FoodUuid,
    string FoodName,
    decimal Grams,
    NutritionalValue Nutrition,
    string Reason);

public record ShakeAdjustmentResult(
    bool NoAdjustmentNeeded,
    decimal ScaleFactor,
    NutritionalValue BaseNutrition,
    NutritionalValue ScaledNutrition,
    IReadOnlyList<ShakeAdjustedIngredient> Ingredients,
    ShakeExtraItem? ExtraItem,
    DailyGap ResultingGap);

public static class ShakeAdjustmentCalculator
{
    public const decimal MinScaleFactor = 0.5m;
    public const decimal MaxScaleFactor = 2.0m;

    public static ShakeAdjustmentResult Calculate(
        DailyGap currentGap,
        IReadOnlyList<(FoodEntity Food, decimal ServingGrams)> templateIngredients,
        bool isPlatePoor,
        IReadOnlyList<FoodEntity>? favoriteFoods = null)
    {
        if (currentGap is null)
        {
            throw new ArgumentNullException(nameof(currentGap));
        }

        // 1. If gap is already closed, no adjustment needed
        if (currentGap.CalorieGap <= 0 && currentGap.ProteinGap <= 0)
        {
            return new ShakeAdjustmentResult(
                NoAdjustmentNeeded: true,
                ScaleFactor: 0m,
                BaseNutrition: NutritionalValue.Zero,
                ScaledNutrition: NutritionalValue.Zero,
                Ingredients: Array.Empty<ShakeAdjustedIngredient>(),
                ExtraItem: null,
                ResultingGap: currentGap);
        }

        // 2. Compute base template nutrition
        var baseNutrition = NutritionalValue.Zero;
        if (templateIngredients != null)
        {
            foreach (var (food, grams) in templateIngredients)
            {
                if (food != null && grams > 0)
                {
                    baseNutrition += NutritionalValue.FromPer100G(food.Calories, food.Protein, food.Carbohydrates, food.Fat, grams);
                }
            }
        }

        if (baseNutrition.Calories <= 0)
        {
            return new ShakeAdjustmentResult(
                NoAdjustmentNeeded: true,
                ScaleFactor: 0m,
                BaseNutrition: NutritionalValue.Zero,
                ScaledNutrition: NutritionalValue.Zero,
                Ingredients: Array.Empty<ShakeAdjustedIngredient>(),
                ExtraItem: null,
                ResultingGap: currentGap);
        }

        // 3. Determine scale factor
        var factorKcal = currentGap.CalorieGap / baseNutrition.Calories;
        var factorProtein = baseNutrition.Protein > 0 ? currentGap.ProteinGap / baseNutrition.Protein : 0m;

        decimal targetFactor;
        if (isPlatePoor && factorProtein > factorKcal)
        {
            // Prioritize protein when day's cafeteria plate was poor
            targetFactor = factorProtein;
        }
        else
        {
            targetFactor = factorKcal;
        }

        var scaleFactor = Math.Clamp(Math.Round(targetFactor, 2), MinScaleFactor, MaxScaleFactor);

        // 4. Scale template ingredients
        var ingredients = new List<ShakeAdjustedIngredient>();
        var scaledNutrition = NutritionalValue.Zero;

        if (templateIngredients != null)
        {
            foreach (var (food, grams) in templateIngredients)
            {
                var scaledGrams = Math.Round(grams * scaleFactor, 1);
                var itemNutrition = food != null
                    ? NutritionalValue.FromPer100G(food.Calories, food.Protein, food.Carbohydrates, food.Fat, scaledGrams)
                    : NutritionalValue.Zero;

                scaledNutrition += itemNutrition;
                ingredients.Add(new ShakeAdjustedIngredient(
                    food?.Uuid ?? Guid.Empty,
                    food?.Name ?? string.Empty,
                    grams,
                    scaledGrams,
                    itemNutrition));
            }
        }

        // 5. Remaining gap after scaled shake
        var remCalories = Math.Max(0m, currentGap.CalorieGap - scaledNutrition.Calories);
        var remProtein = Math.Max(0m, currentGap.ProteinGap - scaledNutrition.Protein);

        // 6. Check for extra favorite food if at 2.0x max limit and deficit remains
        ShakeExtraItem? extraItem = null;
        if (scaleFactor >= MaxScaleFactor && (remCalories > 0 || remProtein > 0) && favoriteFoods != null && favoriteFoods.Count > 0)
        {
            if (remProtein > 0)
            {
                var bestProteinFood = favoriteFoods.OrderByDescending(f => f.Protein).FirstOrDefault(f => f.Protein > 0);
                if (bestProteinFood != null)
                {
                    var extraGrams = Math.Round((remProtein / bestProteinFood.Protein) * 100m, 1);
                    var extraNutrition = NutritionalValue.FromPer100G(
                        bestProteinFood.Calories,
                        bestProteinFood.Protein,
                        bestProteinFood.Carbohydrates,
                        bestProteinFood.Fat,
                        extraGrams);

                    extraItem = new ShakeExtraItem(
                        bestProteinFood.Uuid,
                        bestProteinFood.Name,
                        extraGrams,
                        extraNutrition,
                        "Completar meta de proteína");
                }
            }
            else if (remCalories > 0)
            {
                var bestCalorieFood = favoriteFoods.OrderByDescending(f => f.Calories).FirstOrDefault(f => f.Calories > 0);
                if (bestCalorieFood != null)
                {
                    var extraGrams = Math.Round((remCalories / bestCalorieFood.Calories) * 100m, 1);
                    var extraNutrition = NutritionalValue.FromPer100G(
                        bestCalorieFood.Calories,
                        bestCalorieFood.Protein,
                        bestCalorieFood.Carbohydrates,
                        bestCalorieFood.Fat,
                        extraGrams);

                    extraItem = new ShakeExtraItem(
                        bestCalorieFood.Uuid,
                        bestCalorieFood.Name,
                        extraGrams,
                        extraNutrition,
                        "Completar meta calórica");
                }
            }
        }

        // 7. Resulting gap
        var totalShakeAddition = scaledNutrition + (extraItem?.Nutrition ?? NutritionalValue.Zero);
        var newConsumed = currentGap.Consumed + totalShakeAddition;

        var resultingGap = NutritionCalculator.CalculateDailyGap(
            currentGap.CalorieTarget,
            currentGap.ProteinTarget,
            newConsumed,
            currentGap.GuaranteedCafeteria);

        return new ShakeAdjustmentResult(
            NoAdjustmentNeeded: false,
            ScaleFactor: scaleFactor,
            BaseNutrition: baseNutrition,
            ScaledNutrition: scaledNutrition,
            Ingredients: ingredients,
            ExtraItem: extraItem,
            ResultingGap: resultingGap);
    }
}

