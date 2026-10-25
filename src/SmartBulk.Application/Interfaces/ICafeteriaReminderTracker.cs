namespace SmartBulk.Application.Interfaces;

public interface ICafeteriaReminderTracker
{
    bool HasBeenSent(DateOnly date, string mealType);
    void MarkAsSent(DateOnly date, string mealType);
    void Reset();
}

