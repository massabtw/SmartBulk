using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.OpenFoodFacts;

namespace SmartBulk.Infrastructure.Http.OpenFoodFacts;

internal class OpenFoodFactsConfigureOptions : IConfigureOptions<OpenFoodFactsOptions>
{
    private readonly IConfiguration _configuration;

    public OpenFoodFactsConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(OpenFoodFactsOptions options)
    {
        _configuration.GetSection(OpenFoodFactsOptions.SectionName).Bind(options);
    }
}

