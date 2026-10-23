using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatResponseFormatModel
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}
