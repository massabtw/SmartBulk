namespace SmartBulk.Domain.Entities;

public class MealTemplateEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<MealTemplateItemEntity> Items { get; set; } = new();
}

