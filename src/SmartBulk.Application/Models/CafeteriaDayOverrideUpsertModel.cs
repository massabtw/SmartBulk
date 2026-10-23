namespace SmartBulk.Application.Models;

public class CafeteriaDayOverrideUpsertModel
{
    public DateOnly Date { get; set; }
    public bool HasCafeteria { get; set; }
    public string? Reason { get; set; }
}
