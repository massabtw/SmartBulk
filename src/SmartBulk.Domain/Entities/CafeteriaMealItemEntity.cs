namespace SmartBulk.Domain.Entities;

public class CafeteriaMealItemEntity
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int CafeteriaMealId { get; set; }
    public int? FoodId { get; set; }
    public string RawName { get; set; } = string.Empty;
    public decimal ServingGrams { get; set; }
    public bool IsPending { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    public FoodEntity? Food { get; set; }
}
