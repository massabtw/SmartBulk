using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Application.Interfaces;
using SmartBulk.Domain.Http.Telegram;

namespace SmartBulk.Api.BackgroundServices;

public class TelegramPollingHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramPollingHostedService> _logger;

    public TelegramPollingHostedService(
        IServiceProvider serviceProvider,
        IOptions<TelegramOptions> options,
        ILogger<TelegramPollingHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.UsePolling || string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogInformation("Telegram polling is disabled or BotToken is not set.");
            return;
        }

        _logger.LogInformation("Starting Telegram polling background service for bot...");

        long? offset = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
                var handler = scope.ServiceProvider.GetRequiredService<ITelegramUpdateHandler>();

                var updatesResult = await client.GetUpdatesAsync(offset, limit: 100, timeoutSeconds: 30, cancellationToken: stoppingToken);
                if (updatesResult.Success && updatesResult.Data != null)
                {
                    foreach (var update in updatesResult.Data)
                    {
                        try
                        {
                            await handler.HandleUpdateAsync(update, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling Telegram update {UpdateId}", update.UpdateId);
                        }

                        offset = update.UpdateId + 1;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Telegram polling iteration.");
                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Telegram polling service stopped.");
    }
}

