using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class CafeteriaDayOverrideModelMap
{
    public static CafeteriaDayOverrideModel? ToModel(this CafeteriaDayOverrideEntity? entity)
    {
        if (entity is null) return null;

        var isNoDinner = entity.Reason != null &&
                         entity.Reason.Contains("NoDinner", StringComparison.OrdinalIgnoreCase);

        return new CafeteriaDayOverrideModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            HasCafeteria = entity.HasCafeteria,
            IncludeDinner = entity.HasCafeteria && !isNoDinner,
            Reason = entity.Reason
        };
    }

    public static CafeteriaDayOverrideEntity ToEntity(this CafeteriaDayOverrideUpsertModel model)
    {
        var reason = model.Reason;
        if (model.IncludeDinner == false && (string.IsNullOrWhiteSpace(reason) || !reason.Contains("NoDinner", StringComparison.OrdinalIgnoreCase)))
        {
            reason = string.IsNullOrWhiteSpace(reason) ? "NoDinner" : $"{reason};NoDinner";
        }
        else if (model.IncludeDinner == true && string.Equals(reason, "NoDinner", StringComparison.OrdinalIgnoreCase))
        {
            reason = null;
        }

        return new CafeteriaDayOverrideEntity
        {
            Date = model.Date,
            HasCafeteria = model.HasCafeteria,
            Reason = reason
        };
    }
}
