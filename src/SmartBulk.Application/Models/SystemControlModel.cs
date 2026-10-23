namespace SmartBulk.Application.Models;

public class SystemControlModel
{
    public Guid Uuid { get; set; }
    public string SystemKey { get; set; } = string.Empty;
    public string SystemValue { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

