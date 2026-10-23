namespace SmartBulk.Domain.Entities;

public class CreatineLogEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public bool Taken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
}
