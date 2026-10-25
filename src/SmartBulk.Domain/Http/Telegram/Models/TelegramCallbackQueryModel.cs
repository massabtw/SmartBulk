using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramCallbackQueryModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramChatModel? From { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageModel? Message { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

