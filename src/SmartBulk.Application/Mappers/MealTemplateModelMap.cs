using SmartBulk.Application.Models;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class MealTemplateModelMap
{
    public static MealTemplateModel? ToModel(this MealTemplateEntity? entity)
    {
        if (entity is null) return null;

        var items = entity.Items?.Select(i => i.ToItemModel()!).Where(i => i != null).ToList() ?? new();

        var totalNutrition = items.Aggregate(
            NutritionalValue.Zero,
            (acc, cur) => acc.Add(new NutritionalValue(cur.Calories, cur.Protein, cur.Carbohydrates, cur.Fat)));

        return new MealTemplateModel
        {
            Uuid = entity.Uuid,
            Name = entity.Name,
            MealType = entity.MealType,
            TotalCalories = Math.Round(totalNutrition.Calories, 2),
            TotalProtein = Math.Round(totalNutrition.Protein, 2),
            TotalCarbohydrates = Math.Round(totalNutrition.Carbohydrates, 2),
            TotalFat = Math.Round(totalNutrition.Fat, 2),
            Items = items
        };
    }

    public static IEnumerable<MealTemplateModel> ToModel(this IEnumerable<MealTemplateEntity> entities)
    {
        return entities?.Select(e => e.ToModel()!).Where(m => m != null) ?? Enumerable.Empty<MealTemplateModel>();
    }

    public static MealTemplateItemModel? ToItemModel(this MealTemplateItemEntity? entity)
    {
        if (entity is null) return null;

        var food = entity.Food;
        var nutrition = food is null
            ? NutritionalValue.Zero
            : NutritionalValue.FromPer100G(food.Calories, food.Protein, food.Carbohydrates, food.Fat, entity.ServingGrams);

        return new MealTemplateItemModel
        {
            Uuid = entity.Uuid,
            FoodUuid = food?.Uuid ?? Guid.Empty,
            FoodName = food?.Name ?? string.Empty,
            ServingGrams = entity.ServingGrams,
            Calories = nutrition.Calories,
            Protein = nutrition.Protein,
            Carbohydrates = nutrition.Carbohydrates,
            Fat = nutrition.Fat
        };
    }

    public static MealTemplateEntity ToEntity(this MealTemplateCreateModel model, List<MealTemplateItemEntity> items)
    {
        return new MealTemplateEntity
        {
            Uuid = Guid.NewGuid(),
            Name = model.Name.Trim(),
            MealType = MealTemplateTypes.Normalize(model.MealType),
            Items = items
        };
    }

    public static MealTemplateEntity ToEntity(this MealTemplateUpdateModel model, int id, Guid uuid, List<MealTemplateItemEntity> items)
    {
        return new MealTemplateEntity
        {
            Id = id,
            Uuid = uuid,
            Name = model.Name.Trim(),
            MealType = MealTemplateTypes.Normalize(model.MealType),
            Items = items
        };
    }

    public static MealTemplateItemEntity ToEntity(this MealTemplateItemCreateModel model, int templateId, FoodEntity food)
    {
        return new MealTemplateItemEntity
        {
            Uuid = Guid.NewGuid(),
            MealTemplateId = templateId,
            FoodId = food.Id,
            ServingGrams = model.ServingGrams,
            Food = food
        };
    }
}

