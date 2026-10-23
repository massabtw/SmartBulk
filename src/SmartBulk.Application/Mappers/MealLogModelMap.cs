using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

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
}
