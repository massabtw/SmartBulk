namespace SmartBulk.Application.Models;

public class CafeteriaMealItemCreateModel
{
    public string RawName { get; set; } = string.Empty;
    public decimal ServingGrams { get; set; }
    public Guid? FoodUuid { get; set; }
}
