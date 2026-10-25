namespace SmartBulk.Domain.Http.Llm.Models;

public class CafeteriaLlmExtractionModel
{
    public List<CafeteriaLlmMealExtractionModel> Meals { get; set; } = new();
}

