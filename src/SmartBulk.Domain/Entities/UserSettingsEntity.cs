namespace SmartBulk.Domain.Entities;

public class UserSettingsEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int CalorieTargetKcal { get; set; }
    public decimal ProteinTargetGPerKg { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public TimeSpan LunchReminderTime { get; set; }
    public TimeSpan DinnerReminderTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
}
