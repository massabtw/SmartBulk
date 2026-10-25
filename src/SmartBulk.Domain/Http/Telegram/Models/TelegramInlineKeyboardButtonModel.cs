using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramInlineKeyboardButtonModel
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("callback_data")]
    public string? CallbackData { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

