namespace SmartBulk.Application.Models;

public class NaturalLanguageMealLogRequestModel
{
    public string Text { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageMimeType { get; set; }
}

