namespace SmartBulk.Domain.Http.Llm.Models;

public class CafeteriaLlmMealExtractionModel
{
    public string Date { get; set; } = string.Empty;
    public string MealType { get; set; } = "Lunch";
    public List<CafeteriaLlmItemExtractionModel> Items { get; set; } = new();
}

