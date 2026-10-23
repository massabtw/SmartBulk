using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Mappers;

public static class UserSettingsModelMap
{
    public static UserSettingsModel? ToModel(this UserSettingsEntity? entity)
    {
        if (entity is null)
            return null;

        return new UserSettingsModel
        {
            Uuid = entity.Uuid,
            CalorieTargetKcal = entity.CalorieTargetKcal,
            ProteinTargetGPerKg = entity.ProteinTargetGPerKg,
            TimeZoneId = entity.TimeZoneId,
            LunchReminderTime = entity.LunchReminderTime.ToString(@"hh\:mm"),
            DinnerReminderTime = entity.DinnerReminderTime.ToString(@"hh\:mm")
        };
    }

    public static UserSettingsEntity? ToEntity(this UserSettingsModel? model)
    {
        if (model is null)
            return null;

        return new UserSettingsEntity
        {
            Uuid = model.Uuid,
            CalorieTargetKcal = model.CalorieTargetKcal,
            ProteinTargetGPerKg = model.ProteinTargetGPerKg,
            TimeZoneId = model.TimeZoneId,
            LunchReminderTime = ParseTimeSpan(model.LunchReminderTime),
            DinnerReminderTime = ParseTimeSpan(model.DinnerReminderTime)
        };
    }

    public static IEnumerable<UserSettingsModel> ToModel(this IEnumerable<UserSettingsEntity> entities)
    {
        return entities?.Select(e => e.ToModel()!).Where(m => m != null) ?? Enumerable.Empty<UserSettingsModel>();
    }

    private static TimeSpan ParseTimeSpan(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return TimeSpan.Zero;

        if (TimeSpan.TryParse(time, out var parsed))
            return parsed;

        return TimeSpan.Zero;
    }
}
