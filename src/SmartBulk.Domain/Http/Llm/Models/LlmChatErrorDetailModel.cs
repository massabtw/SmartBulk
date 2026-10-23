using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatErrorDetailModel
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
