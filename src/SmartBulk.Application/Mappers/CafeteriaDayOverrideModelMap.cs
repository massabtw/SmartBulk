using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class CafeteriaDayOverrideModelMap
{
    public static CafeteriaDayOverrideModel? ToModel(this CafeteriaDayOverrideEntity? entity)
    {
        if (entity is null) return null;

        return new CafeteriaDayOverrideModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            HasCafeteria = entity.HasCafeteria,
            Reason = entity.Reason
        };
    }

    public static CafeteriaDayOverrideEntity ToEntity(this CafeteriaDayOverrideUpsertModel model)
    {
        return new CafeteriaDayOverrideEntity
        {
            Date = model.Date,
            HasCafeteria = model.HasCafeteria,
            Reason = model.Reason
        };
    }
}
