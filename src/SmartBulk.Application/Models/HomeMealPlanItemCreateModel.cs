namespace SmartBulk.Application.Models;

public class HomeMealPlanItemCreateModel
{
    public DateOnly Date { get; set; }
    public string MealSlot { get; set; } = string.Empty;
    public Guid MealTemplateUuid { get; set; }
}

