using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatCompletionRequestModel
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o";

    [JsonPropertyName("messages")]
    public List<LlmChatMessageModel> Messages { get; set; } = new();

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LlmChatResponseFormatModel? ResponseFormat { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Temperature { get; set; }
}
