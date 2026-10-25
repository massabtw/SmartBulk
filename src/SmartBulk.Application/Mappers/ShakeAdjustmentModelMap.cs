using SmartBulk.Application.Models;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class ShakeAdjustmentModelMap
{
    public static ShakeAdjustmentSuggestionModel ToModel(this ShakeAdjustmentResult result)
    {
        return new ShakeAdjustmentSuggestionModel
        {
            NoAdjustmentNeeded = result.NoAdjustmentNeeded,
            ScaleFactor = result.ScaleFactor,
            BaseCalories = Math.Round(result.BaseNutrition.Calories, 1),
            BaseProtein = Math.Round(result.BaseNutrition.Protein, 1),
            ScaledCalories = Math.Round(result.ScaledNutrition.Calories, 1),
            ScaledProtein = Math.Round(result.ScaledNutrition.Protein, 1),
            Ingredients = result.Ingredients.Select(i => i.ToModel()).ToList(),
            ExtraItem = result.ExtraItem?.ToModel(),
            ResultingCalorieGap = result.ResultingGap.CalorieGap,
            ResultingProteinGap = result.ResultingGap.ProteinGap
        };
    }

    public static ShakeAdjustedIngredientModel ToModel(this ShakeAdjustedIngredient ingredient)
    {
        return new ShakeAdjustedIngredientModel
        {
            FoodUuid = ingredient.FoodUuid,
            FoodName = ingredient.FoodName,
            OriginalGrams = ingredient.OriginalGrams,
            ScaledGrams = ingredient.ScaledGrams,
            Calories = ingredient.ScaledNutrition.Calories,
            Protein = ingredient.ScaledNutrition.Protein,
            Carbohydrates = ingredient.ScaledNutrition.Carbohydrates,
            Fat = ingredient.ScaledNutrition.Fat
        };
    }

    public static ShakeExtraItemModel? ToModel(this ShakeExtraItem? extraItem)
    {
        if (extraItem is null) return null;

        return new ShakeExtraItemModel
        {
            FoodUuid = extraItem.FoodUuid,
            FoodName = extraItem.FoodName,
            Grams = extraItem.Grams,
            Calories = extraItem.Nutrition.Calories,
            Protein = extraItem.Nutrition.Protein,
            Carbohydrates = extraItem.Nutrition.Carbohydrates,
            Fat = extraItem.Nutrition.Fat,
            Reason = extraItem.Reason
        };
    }
}

