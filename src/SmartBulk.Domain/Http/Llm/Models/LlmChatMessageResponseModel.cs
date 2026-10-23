using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatMessageResponseModel
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "assistant";

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
