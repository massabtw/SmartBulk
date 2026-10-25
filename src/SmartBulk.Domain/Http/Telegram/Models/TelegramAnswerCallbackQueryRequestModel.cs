using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramAnswerCallbackQueryRequestModel
{
    [JsonPropertyName("callback_query_id")]
    public string CallbackQueryId { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

