using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramUpdateModel
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageModel? Message { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQueryModel? CallbackQuery { get; set; }
}

