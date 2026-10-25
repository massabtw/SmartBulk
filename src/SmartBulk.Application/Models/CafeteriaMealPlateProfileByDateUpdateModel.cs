namespace SmartBulk.Application.Models;

public class CafeteriaMealPlateProfileByDateUpdateModel
{
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = "Lunch";
    public string PlateProfile { get; set; } = "Standard";
}

