using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.Llm;

namespace SmartBulk.Infrastructure.Http.Llm;

internal class LlmAuthHandler : DelegatingHandler
{
    private readonly LlmOptions _options;

    public LlmAuthHandler(IOptions<LlmOptions> options)
    {
        _options = options.Value;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization == null && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        return base.Send(request, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization == null && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
