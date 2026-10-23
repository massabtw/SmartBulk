using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Domain.Entities;

public class CafeteriaMealEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int CafeteriaMenuId { get; set; }
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = "Lunch"; // "Lunch", "Dinner"
    public string PlateProfile { get; set; } = "Standard"; // "Standard", "Generous", "Small"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<CafeteriaMealItemEntity> Items { get; set; } = new();
}
