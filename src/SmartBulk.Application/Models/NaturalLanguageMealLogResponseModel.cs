namespace SmartBulk.Application.Models;

public class NaturalLanguageMealLogResponseModel
{
    public bool Success { get; set; }
    public bool HasUnmatchedItems { get; set; }
    public string Message { get; set; } = string.Empty;

    public Guid? MealLogUuid { get; set; }
    public string? MealName { get; set; }
    public string? Summary { get; set; }
    public decimal? TotalCalories { get; set; }
    public decimal? TotalProtein { get; set; }
    public decimal? TotalCarbohydrates { get; set; }
    public decimal? TotalFat { get; set; }
    public DailyGapOverviewModel? NewDailyGap { get; set; }

    public List<string> UnmatchedItems { get; set; } = new();
    public string? Question { get; set; }
}

