using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramFileResponseModel
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public TelegramFileModel? Result { get; set; }
}

