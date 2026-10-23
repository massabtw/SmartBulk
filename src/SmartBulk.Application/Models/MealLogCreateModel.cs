namespace SmartBulk.Application.Models;

public class MealLogItemCreateModel
{
    public Guid FoodUuid { get; set; }
    public decimal ServingGrams { get; set; }
}

public class MealLogCreateModel
{
    public DateOnly? Date { get; set; }
    public DateTimeOffset? LoggedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MealLogItemCreateModel> Items { get; set; } = new();
}
