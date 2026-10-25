using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.Telegram;

namespace SmartBulk.Infrastructure.Http.Telegram;

public class TelegramConfigureOptions : IConfigureOptions<TelegramOptions>
{
    private readonly IConfiguration _configuration;

    public TelegramConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(TelegramOptions options)
    {
        _configuration.GetSection(TelegramOptions.SectionName).Bind(options);
    }
}

