namespace SmartBulk.Application.Models;

public class CafeteriaMenuIngestModel
{
    public string? Text { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageMimeType { get; set; }
    public DateOnly? WeekStartDate { get; set; }
}
