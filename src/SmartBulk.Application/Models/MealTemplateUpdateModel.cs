namespace SmartBulk.Application.Models;

public class MealTemplateUpdateModel
{
    public string Name { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public List<MealTemplateItemCreateModel> Items { get; set; } = new();
}

