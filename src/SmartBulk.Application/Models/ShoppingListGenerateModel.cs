namespace SmartBulk.Application.Models;

public class ShoppingListGenerateModel
{
    public DateOnly? WeekStartDate { get; set; }
    public bool ForceRegeneratePlan { get; set; } = false;
}

