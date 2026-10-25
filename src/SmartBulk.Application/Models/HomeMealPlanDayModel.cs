namespace SmartBulk.Application.Models;

public class HomeMealPlanDayModel
{
    public DateOnly Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool HasCafeteria { get; set; }
    public List<string> RequiredSlots { get; set; } = new();
    public List<string> MissingSlots { get; set; } = new();
    public bool IsComplete { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public List<HomeMealPlanItemModel> Items { get; set; } = new();
}

