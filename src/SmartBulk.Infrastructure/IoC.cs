using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.OpenFoodFacts;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.UnitOfWorks;
using SmartBulk.Infrastructure.Http.Llm;
using SmartBulk.Infrastructure.Http.OpenFoodFacts;
using SmartBulk.Infrastructure.Http.Telegram;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure;

public static class IoC
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IUnitOfWorkApp, UnitOfWorkApp>();

        services.AddLlm(configuration);
        services.AddTelegram(configuration);
        services.AddOpenFoodFacts(configuration);

        return services;
    }

    private static void AddOpenFoodFacts(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOptions<OpenFoodFactsConfigureOptions>();
        services.AddHttpClient<IFoodExternalProvider, OpenFoodFactsClient>();
    }

    private static void AddLlm(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOptions<LlmConfigureOptions>();
        services.AddTransient<LlmAuthHandler>();
        services.AddHttpClient<ILlmClient, LlmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddHttpMessageHandler<LlmAuthHandler>();
    }

    private static void AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOptions<TelegramConfigureOptions>();
        services.AddHttpClient<ITelegramClient, TelegramClient>();
    }
}
