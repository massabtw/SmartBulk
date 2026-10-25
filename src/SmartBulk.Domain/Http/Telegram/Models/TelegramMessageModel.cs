using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramMessageModel
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatModel? Chat { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("photo")]
    public List<TelegramPhotoSizeModel>? Photo { get; set; }
}

