namespace SmartBulk.Application.Models;

public class CafeteriaMealModel
{
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = "Lunch";
    public string PlateProfile { get; set; } = "Standard";
    public List<CafeteriaMealItemModel> Items { get; set; } = new();

    public decimal TotalCalories => Items.Sum(i => i.Calories);
    public decimal TotalProtein => Items.Sum(i => i.Protein);
    public decimal TotalCarbohydrates => Items.Sum(i => i.Carbohydrates);
    public decimal TotalFat => Items.Sum(i => i.Fat);
}
