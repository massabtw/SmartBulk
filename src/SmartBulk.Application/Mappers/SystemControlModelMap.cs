using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class SystemControlModelMap
{
    public static SystemControlModel? ToModel(this SystemControlEntity? entity)
    {
        if (entity is null)
            return null;

        return new SystemControlModel
        {
            Uuid = entity.Uuid,
            SystemKey = entity.SystemKey,
            SystemValue = entity.SystemValue,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static SystemControlEntity ToEntity(this SystemControlModel model)
    {
        return new SystemControlEntity
        {
            Uuid = model.Uuid,
            SystemKey = model.SystemKey,
            SystemValue = model.SystemValue,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public static IEnumerable<SystemControlModel> ToModel(this IEnumerable<SystemControlEntity>? entities)
    {
        if (entities is null)
            return Enumerable.Empty<SystemControlModel>();

        return entities.Select(e => e.ToModel()!).Where(m => m is not null);
    }
}

