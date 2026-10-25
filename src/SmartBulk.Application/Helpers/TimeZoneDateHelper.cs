using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Helpers;

public static class TimeZoneDateHelper
{
    public const string DefaultTimeZoneId = "America/Sao_Paulo";

    public static async Task<DateOnly> GetUserTodayAsync(IUnitOfWorkApp unitOfWork, CancellationToken cancellationToken = default)
    {
        string? timeZoneId = null;
        if (unitOfWork.UserSettings != null)
        {
            var settings = await unitOfWork.UserSettings.GetAsync(cancellationToken);
            timeZoneId = settings?.TimeZoneId;
        }

        return GetTodayInTimeZone(timeZoneId);
    }

    public static DateOnly GetTodayInTimeZone(string? timeZoneId)
    {
        var localTime = ToLocalTime(DateTimeOffset.UtcNow, timeZoneId);
        return DateOnly.FromDateTime(localTime.DateTime);
    }

    public static DateTimeOffset ToLocalTime(DateTimeOffset dateTimeOffset, string? timeZoneId)
    {
        var tzId = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTime(dateTimeOffset, tz);
        }
        catch
        {
            // Fallback to UTC-3 (Brasília standard offset) if system timezone database doesn't resolve ID
            return dateTimeOffset.ToOffset(TimeSpan.FromHours(-3));
        }
    }

    public static DateOnly GetWeekMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
