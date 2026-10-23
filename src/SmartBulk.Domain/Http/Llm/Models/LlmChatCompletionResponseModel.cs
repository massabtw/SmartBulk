using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatCompletionResponseModel
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<LlmChatChoiceModel> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public LlmChatUsageModel? Usage { get; set; }
}
