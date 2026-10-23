using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Llm.Models;

public class LlmChatErrorResponseModel
{
    [JsonPropertyName("error")]
    public LlmChatErrorDetailModel? Error { get; set; }
}
