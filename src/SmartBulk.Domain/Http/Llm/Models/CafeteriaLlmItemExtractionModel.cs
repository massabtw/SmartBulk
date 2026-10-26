namespace SmartBulk.Domain.Http.Llm.Models;

public class CafeteriaLlmItemExtractionModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Grams { get; set; }
    public decimal? EstimatedKcalPer100g { get; set; }
    public decimal? EstimatedProteinPer100g { get; set; }
    public decimal? EstimatedCarbsPer100g { get; set; }
    public decimal? EstimatedFatPer100g { get; set; }
}
