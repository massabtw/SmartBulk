using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealLogController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] IMealLogService service,
        [FromBody] MealLogCreateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateMealLogAsync(model, cancellationToken);
        if (result.Success && result.Data != null)
        {
            return result.Created($"/api/MealLog/{result.Data.Uuid}");
        }

        return result.Created();
    }

    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetByUuid(
        [FromServices] IMealLogService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMealLogByUuidAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate(
        [FromServices] IMealLogService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetMealLogsByDateAsync(targetDate, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("last")]
    public async Task<IActionResult> DeleteLatest(
        [FromServices] IMealLogService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteLatestMealLogAsync(cancellationToken);
        return result.Ok();
    }

    [HttpDelete("{uuid}")]
    public async Task<IActionResult> Delete(
        [FromServices] IMealLogService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteMealLogAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("items/{itemUuid}")]
    public async Task<IActionResult> DeleteItem(
        [FromServices] IMealLogService service,
        [FromRoute] Guid itemUuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteMealLogItemAsync(itemUuid, cancellationToken);
        return result.Ok();
    }

    [HttpGet("daily-gap")]
    public async Task<IActionResult> GetDailyGap(
        [FromServices] IMealLogService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetDailyGapOverviewAsync(targetDate, cancellationToken);
        return result.Ok();
    }
}
