using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Domain.Http.Llm;

public interface ILlmClient
{
    Task<Result<string>> GenerateCompletionAsync(
        string prompt,
        string? systemMessage = null,
        bool jsonMode = false,
        string? imageBase64 = null,
        string? imageMimeType = null,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatCompletionResponseModel>> SendChatAsync(
        LlmChatCompletionRequestModel request,
        CancellationToken cancellationToken = default);
}
