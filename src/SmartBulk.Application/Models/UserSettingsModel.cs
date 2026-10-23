namespace SmartBulk.Application.Models;

public class UserSettingsModel
{
    public Guid Uuid { get; set; }
    public int CalorieTargetKcal { get; set; }
    public decimal ProteinTargetGPerKg { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public string LunchReminderTime { get; set; } = string.Empty;
    public string DinnerReminderTime { get; set; } = string.Empty;
}
