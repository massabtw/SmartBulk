namespace SmartBulk.Domain.Entities;

public class SystemControlEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public string SystemKey { get; set; } = string.Empty;
    public string SystemValue { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
}

