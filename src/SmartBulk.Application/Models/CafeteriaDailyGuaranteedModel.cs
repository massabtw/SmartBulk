namespace SmartBulk.Application.Models;

public class CafeteriaDailyGuaranteedModel
{
    public DateOnly Date { get; set; }
    public bool HasCafeteria { get; set; }
    public bool HasConfirmedMenu { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public List<CafeteriaMealGuaranteedModel> Meals { get; set; } = new();
}

public class CafeteriaMealGuaranteedModel
{
    public Guid MealUuid { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string PlateProfile { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
}
