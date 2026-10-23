namespace SmartBulk.Application.Models;

public class CafeteriaLlmExtractionModel
{
    public List<CafeteriaLlmMealExtractionModel> Meals { get; set; } = new();
}

public class CafeteriaLlmMealExtractionModel
{
    public string Date { get; set; } = string.Empty;
    public string MealType { get; set; } = "Lunch";
    public List<CafeteriaLlmItemExtractionModel> Items { get; set; } = new();
}

public class CafeteriaLlmItemExtractionModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Grams { get; set; }
}
