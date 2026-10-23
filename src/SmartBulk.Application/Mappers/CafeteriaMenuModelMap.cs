using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class CafeteriaMenuModelMap
{
    public static CafeteriaMenuModel? ToModel(this CafeteriaMenuEntity? entity)
    {
        if (entity is null)
            return null;

        return new CafeteriaMenuModel
        {
            Uuid = entity.Uuid,
            WeekStartDate = entity.WeekStartDate,
            Status = entity.Status,
            Meals = entity.Meals?.Select(m => m.ToModel()!).Where(m => m != null).ToList() ?? new List<CafeteriaMealModel>()
        };
    }

    public static CafeteriaMealModel? ToModel(this CafeteriaMealEntity? entity)
    {
        if (entity is null)
            return null;

        return new CafeteriaMealModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            MealType = entity.MealType,
            PlateProfile = entity.PlateProfile,
            Items = entity.Items?.Select(i => i.ToModel()!).Where(i => i != null).ToList() ?? new List<CafeteriaMealItemModel>()
        };
    }

    public static CafeteriaMealItemModel? ToModel(this CafeteriaMealItemEntity? entity)
    {
        if (entity is null)
            return null;

        var model = new CafeteriaMealItemModel
        {
            Uuid = entity.Uuid,
            RawName = entity.RawName,
            ServingGrams = entity.ServingGrams,
            IsPending = entity.IsPending
        };

        if (entity.Food != null)
        {
            model.FoodUuid = entity.Food.Uuid;
            model.MatchedFoodName = entity.Food.Name;
            var portion = NutritionalValue.FromPer100G(
                entity.Food.Calories,
                entity.Food.Protein,
                entity.Food.Carbohydrates,
                entity.Food.Fat,
                entity.ServingGrams);

            model.Calories = portion.Calories;
            model.Protein = portion.Protein;
            model.Carbohydrates = portion.Carbohydrates;
            model.Fat = portion.Fat;
        }

        return model;
    }
}
