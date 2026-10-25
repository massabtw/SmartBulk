using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class CreatineLogModelMap
{
    public static CreatineLogModel? ToModel(this CreatineLogEntity? entity)
    {
        if (entity is null)
            return null;

        return new CreatineLogModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            Taken = entity.Taken
        };
    }

    public static CreatineLogEntity? ToEntity(this CreatineLogModel? model)
    {
        if (model is null)
            return null;

        return new CreatineLogEntity
        {
            Uuid = model.Uuid,
            Date = model.Date,
            Taken = model.Taken
        };
    }

    public static CreatineLogEntity ToEntity(this DateOnly date, bool taken)
    {
        return new CreatineLogEntity
        {
            Uuid = Guid.NewGuid(),
            Date = date,
            Taken = taken
        };
    }

    public static IEnumerable<CreatineLogModel> ToModel(this IEnumerable<CreatineLogEntity> entities)
    {
        return entities?.Select(e => e.ToModel()!).Where(m => m != null) ?? Enumerable.Empty<CreatineLogModel>();
    }
}
