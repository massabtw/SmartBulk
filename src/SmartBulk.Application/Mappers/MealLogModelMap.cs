using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class MealLogModelMap
{
    public static MealLogModel? ToModel(this MealLogEntity? entity)
    {
        if (entity is null) return null;

        return new MealLogModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            LoggedAt = entity.LoggedAt,
            Name = entity.Name,
            TotalCalories = entity.TotalCalories,
            TotalProtein = entity.TotalProtein,
            TotalCarbohydrates = entity.TotalCarbohydrates,
            TotalFat = entity.TotalFat,
            Items = entity.Items?.Select(i => i.ToModel()!).Where(i => i != null).ToList() ?? new()
        };
    }

    public static MealLogItemModel? ToModel(this MealLogItemEntity? entity)
    {
        if (entity is null) return null;

        return new MealLogItemModel
        {
            Uuid = entity.Uuid,
            FoodUuid = entity.Food?.Uuid ?? Guid.Empty,
            FoodName = entity.Food?.Name ?? string.Empty,
            ServingGrams = entity.ServingGrams,
            Calories = entity.Calories,
            Protein = entity.Protein,
            Carbohydrates = entity.Carbohydrates,
            Fat = entity.Fat
        };
    }

    public static MealLogItemEntity ToEntity(this MealLogItemCreateModel model, FoodEntity food, NutritionalValue portion)
    {
        return new MealLogItemEntity
        {
            Uuid = Guid.NewGuid(),
            FoodId = food.Id,
            ServingGrams = model.ServingGrams,
            Calories = portion.Calories,
            Protein = portion.Protein,
            Carbohydrates = portion.Carbohydrates,
            Fat = portion.Fat,
            Food = food
        };
    }

    public static MealLogEntity ToEntity(
        this MealLogCreateModel model,
        DateOnly date,
        DateTimeOffset loggedAt,
        NutritionalValue totalNutrition,
        List<MealLogItemEntity> items)
    {
        return new MealLogEntity
        {
            Uuid = Guid.NewGuid(),
            Date = date,
            LoggedAt = loggedAt,
            Name = model.Name.Trim(),
            TotalCalories = Math.Round(totalNutrition.Calories, 2),
            TotalProtein = Math.Round(totalNutrition.Protein, 2),
            TotalCarbohydrates = Math.Round(totalNutrition.Carbohydrates, 2),
            TotalFat = Math.Round(totalNutrition.Fat, 2),
            Items = items
        };
    }

    public static DailyGapOverviewModel ToDailyGapOverviewModel(
        this DailyGap gap,
        DateOnly date,
        decimal? latestWeightKg,
        string? weightWarning,
        IEnumerable<MealLogEntity> meals,
        CafeteriaDailyGuaranteedModel? cafeteria)
    {
        return new DailyGapOverviewModel
        {
            Date = date,
            CalorieTarget = (int)gap.CalorieTarget,
            ProteinTarget = gap.ProteinTarget,
            LatestWeightKg = latestWeightKg,
            WeightWarning = weightWarning,
            ConsumedCalories = gap.Consumed.Calories,
            ConsumedProtein = gap.Consumed.Protein,
            ConsumedCarbohydrates = gap.Consumed.Carbohydrates,
            ConsumedFat = gap.Consumed.Fat,
            CafeteriaCalories = gap.GuaranteedCafeteria.Calories,
            CafeteriaProtein = gap.GuaranteedCafeteria.Protein,
            CafeteriaCarbohydrates = gap.GuaranteedCafeteria.Carbohydrates,
            CafeteriaFat = gap.GuaranteedCafeteria.Fat,
            CalorieGap = gap.CalorieGap,
            ProteinGap = gap.ProteinGap,
            Meals = meals.Select(m => m.ToModel()!).Where(m => m != null).ToList(),
            Cafeteria = cafeteria
        };
    }
}
