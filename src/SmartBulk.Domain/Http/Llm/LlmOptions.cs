namespace SmartBulk.Domain.Http.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "https://models.inference.ai.azure.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
}
