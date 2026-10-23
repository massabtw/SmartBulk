namespace SmartBulk.Domain.Entities;

public class FoodEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public bool IsCustom { get; set; }
    public bool IsFavorite { get; set; }
    public decimal? RawYieldFactor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
}
