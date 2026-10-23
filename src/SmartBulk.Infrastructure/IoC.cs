using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.UnitOfWorks;
using SmartBulk.Infrastructure.Http.Llm;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure;

public static class IoC
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IUnitOfWorkApp, UnitOfWorkApp>();

        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddTransient<LlmAuthHandler>();
        services.AddHttpClient<ILlmClient, LlmClient>()
            .AddHttpMessageHandler<LlmAuthHandler>();

        return services;
    }
}
