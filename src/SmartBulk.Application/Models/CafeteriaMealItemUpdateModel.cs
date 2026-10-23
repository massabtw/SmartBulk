namespace SmartBulk.Application.Models;

public class CafeteriaMealItemUpdateModel
{
    public decimal ServingGrams { get; set; }
    public Guid? FoodUuid { get; set; }
    public string? RawName { get; set; }
}
