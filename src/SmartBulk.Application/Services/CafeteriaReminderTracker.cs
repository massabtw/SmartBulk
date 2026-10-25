using System.Collections.Concurrent;
using SmartBulk.Application.Interfaces;

namespace SmartBulk.Application.Services;

public class CafeteriaReminderTracker : ICafeteriaReminderTracker
{
    private readonly ConcurrentDictionary<string, bool> _sentReminders = new();

    public bool HasBeenSent(DateOnly date, string mealType)
    {
        var key = BuildKey(date, mealType);
        return _sentReminders.ContainsKey(key);
    }

    public void MarkAsSent(DateOnly date, string mealType)
    {
        var key = BuildKey(date, mealType);
        _sentReminders.TryAdd(key, true);
    }

    public void Reset()
    {
        _sentReminders.Clear();
    }

    private static string BuildKey(DateOnly date, string mealType) =>
        $"{date:yyyy-MM-dd}:{mealType.Trim().ToLowerInvariant()}";
}

