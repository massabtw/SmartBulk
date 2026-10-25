namespace SmartBulk.Domain.Http.Llm.Models;

public class NaturalLanguageMealLlmExtractionModel
{
    public string MealName { get; set; } = "Refeição";
    public List<NaturalLanguageMealLlmItemExtractionModel> Items { get; set; } = new();
}

