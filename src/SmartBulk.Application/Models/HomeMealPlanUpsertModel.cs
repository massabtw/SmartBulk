namespace SmartBulk.Application.Models;

public class HomeMealPlanUpsertModel
{
    public DateOnly WeekStartDate { get; set; }
    public List<HomeMealPlanItemCreateModel> Items { get; set; } = new();
}

