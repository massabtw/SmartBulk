namespace SmartBulk.Domain.Entities;

public class CafeteriaDayOverrideEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public bool HasCafeteria { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
}
