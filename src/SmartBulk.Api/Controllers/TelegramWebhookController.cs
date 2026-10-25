using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Application.Interfaces;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/telegram")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    private const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromServices] ITelegramUpdateHandler handler,
        [FromServices] IOptions<TelegramOptions> options,
        [FromServices] ILogger<TelegramWebhookController> logger,
        [FromBody] TelegramUpdateModel update,
        CancellationToken cancellationToken)
    {
        var configuredSecret = options.Value.SecretToken;
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            if (!Request.Headers.TryGetValue(SecretTokenHeader, out var providedSecret) ||
                !string.Equals(providedSecret, configuredSecret, StringComparison.Ordinal))
            {
                logger.LogWarning("Telegram webhook request rejected due to invalid or missing secret token.");
                return StatusCode(StatusCodes.Status403Forbidden, "Invalid secret token.");
            }
        }

        await handler.HandleUpdateAsync(update, cancellationToken);
        return Ok();
    }
}

