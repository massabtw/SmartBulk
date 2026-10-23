namespace SmartBulk.Domain.Entities;

public class CafeteriaMenuEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public string Status { get; set; } = "Draft"; // "Draft", "Confirmed"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<CafeteriaMealEntity> Meals { get; set; } = new();
}
