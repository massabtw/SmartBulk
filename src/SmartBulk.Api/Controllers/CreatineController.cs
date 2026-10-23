using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CreatineController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SetCreatine(
        [FromServices] ICreatineLogService service,
        [FromBody] CreatineLogModel model,
        CancellationToken cancellationToken)
    {
        var date = model.Date == default ? DateOnly.FromDateTime(DateTime.UtcNow) : model.Date;
        var result = await service.SetCreatineAsync(date, model.Taken, cancellationToken);
        return result.Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate(
        [FromServices] ICreatineLogService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetByDateAsync(targetDate, cancellationToken);
        return result.Ok();
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromServices] ICreatineLogService service,
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
