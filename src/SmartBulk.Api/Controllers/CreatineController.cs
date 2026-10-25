using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreatineController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SetCreatine(
        [FromServices] ICreatineLogService service,
        [FromBody] CreatineLogModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.SetCreatineAsync(model, cancellationToken);
        return result.Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate(
        [FromServices] ICreatineLogService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByDateAsync(date, cancellationToken);
        return result.Ok();
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromServices] ICreatineLogService service,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHistoryAsync(startDate, endDate, cancellationToken);
        return result.Ok();
    }
}
