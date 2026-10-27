namespace SmartBulk.Application.Models;

public class HomeMealPlanItemIngredientModel
{
    public string FoodName { get; set; } = string.Empty;
    public decimal ServingGrams { get; set; }
    public string ServingUnitText { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
}

public class HomeMealPlanItemModel
{
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public string MealSlot { get; set; } = string.Empty;
    public Guid MealTemplateUuid { get; set; }
    public string MealTemplateName { get; set; } = string.Empty;
    public string MealTemplateType { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public List<HomeMealPlanItemIngredientModel> Ingredients { get; set; } = new();
}
