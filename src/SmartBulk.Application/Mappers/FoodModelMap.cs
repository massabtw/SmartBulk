using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class FoodModelMap
{
    public static FoodModel? ToModel(this FoodEntity? entity)
    {
        if (entity is null)
            return null;

        return new FoodModel
        {
            Uuid = entity.Uuid,
            Name = entity.Name,
            Calories = entity.Calories,
            Protein = entity.Protein,
            Carbohydrates = entity.Carbohydrates,
            Fat = entity.Fat,
            IsCustom = entity.IsCustom,
            IsFavorite = entity.IsFavorite,
            RawYieldFactor = entity.RawYieldFactor
        };
    }

    public static FoodEntity? ToEntity(this FoodModel? model)
    {
        if (model is null)
            return null;

        return new FoodEntity
        {
            Uuid = model.Uuid,
            Name = model.Name,
            Calories = model.Calories,
            Protein = model.Protein,
            Carbohydrates = model.Carbohydrates,
            Fat = model.Fat,
            IsCustom = model.IsCustom,
            IsFavorite = model.IsFavorite,
            RawYieldFactor = model.RawYieldFactor
        };
    }

    public static IEnumerable<FoodModel> ToModel(this IEnumerable<FoodEntity> entities)
    {
        return entities?.Select(e => e.ToModel()!).Where(m => m != null) ?? Enumerable.Empty<FoodModel>();
    }
}
