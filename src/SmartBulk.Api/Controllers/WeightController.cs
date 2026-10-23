using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeightController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> LogWeight(
        [FromServices] IWeightLogService service,
        [FromBody] WeightLogModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.LogWeightAsync(model, cancellationToken);
        return result.Ok();
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(
        [FromServices] IWeightLogService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLatestAsync(cancellationToken);
        return result.Ok();
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromServices] IWeightLogService service,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-30);

        var result = await service.GetHistoryAsync(start, end, cancellationToken);
        return result.Ok();
    }
}
