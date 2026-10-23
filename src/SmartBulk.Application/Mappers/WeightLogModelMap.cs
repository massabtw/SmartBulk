using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class WeightLogModelMap
{
    public static WeightLogModel? ToModel(this WeightLogEntity? entity)
    {
        if (entity is null)
            return null;

        return new WeightLogModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            WeightKg = entity.WeightKg
        };
    }

    public static WeightLogEntity? ToEntity(this WeightLogModel? model)
    {
        if (model is null)
            return null;

        return new WeightLogEntity
        {
            Uuid = model.Uuid,
            Date = model.Date,
            WeightKg = model.WeightKg
        };
    }

    public static IEnumerable<WeightLogModel> ToModel(this IEnumerable<WeightLogEntity> entities)
    {
        return entities?.Select(e => e.ToModel()!).Where(m => m != null) ?? Enumerable.Empty<WeightLogModel>();
    }
}
