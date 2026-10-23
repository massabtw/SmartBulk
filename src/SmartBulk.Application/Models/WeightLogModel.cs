namespace SmartBulk.Application.Models;

public class WeightLogModel
{
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public decimal WeightKg { get; set; }
}
