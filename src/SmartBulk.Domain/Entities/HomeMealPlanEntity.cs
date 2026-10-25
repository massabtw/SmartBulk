namespace SmartBulk.Domain.Entities;

public class HomeMealPlanEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<HomeMealPlanItemEntity> Items { get; set; } = new();
}

