namespace SmartBulk.Application.Models;

public class HealthModel
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? SystemStatus { get; set; }
}

