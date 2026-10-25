namespace SmartBulk.Application.Models;

public class HomeMealPlanModel
{
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public bool IsWeekComplete { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public List<HomeMealPlanDayModel> Days { get; set; } = new();
}

