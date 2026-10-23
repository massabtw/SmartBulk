using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatMessageModel
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;
}
