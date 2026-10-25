namespace SmartBulk.Domain.Http.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public string SecretToken { get; set; } = string.Empty;
    public bool UsePolling { get; set; }
    public string BaseUrl { get; set; } = "https://api.telegram.org";
}

