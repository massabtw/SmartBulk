namespace SmartBulk.Domain.Http.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string Token
    {
        get => BotToken;
        set { if (!string.IsNullOrWhiteSpace(value)) BotToken = value; }
    }

    public long ChatId { get; set; }
    public long AllowedChatId
    {
        get => ChatId;
        set { if (value != 0) ChatId = value; }
    }
    public string SecretToken { get; set; } = string.Empty;
    public bool UsePolling { get; set; }
    public string BaseUrl { get; set; } = "https://api.telegram.org";
}

