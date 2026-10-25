namespace SmartBulk.Application.Models;

public class CafeteriaDayOverrideModel
{
    public Guid Uuid { get; set; }
    public DateOnly Date { get; set; }
    public bool HasCafeteria { get; set; }
    public bool IncludeDinner { get; set; } = true;
    public string? Reason { get; set; }
}
