namespace SmartBulk.Application.Models;

public class CafeteriaMenuModel
{
    public Guid Uuid { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public string Status { get; set; } = "Draft";
    public List<CafeteriaMealModel> Meals { get; set; } = new();

    public int TotalMeals => Meals.Count;
    public int PendingItemsCount => Meals.SelectMany(m => m.Items).Count(i => i.IsPending);
}
