namespace SmartBulk.Application.Models;

public class MealTemplateModel
{
    public Guid Uuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public List<MealTemplateItemModel> Items { get; set; } = new();
}

