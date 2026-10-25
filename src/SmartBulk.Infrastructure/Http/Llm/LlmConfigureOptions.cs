using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.Llm;

namespace SmartBulk.Infrastructure.Http.Llm;

internal class LlmConfigureOptions : IConfigureOptions<LlmOptions>
{
    private readonly IConfiguration _configuration;

    public LlmConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(LlmOptions options)
    {
        _configuration.GetSection(LlmOptions.SectionName).Bind(options);
    }
}

