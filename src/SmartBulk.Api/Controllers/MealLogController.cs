using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealLogController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] IMealLogService service,
        [FromBody] MealLogCreateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateMealLogAsync(model, cancellationToken);
        return result.Created(data => $"/api/MealLog/{data.Uuid}");
    }

    [HttpPost("natural-language")]
    public async Task<IActionResult> RegisterNaturalLanguage(
        [FromServices] INaturalLanguageMealLogService service,
        [FromBody] NaturalLanguageMealLogRequestModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.ProcessNaturalLanguageMealAsync(model, cancellationToken);
        return result.Ok();
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
        var result = await service.GetMealLogsByDateAsync(date, cancellationToken);
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
        var result = await service.GetDailyGapOverviewAsync(date, cancellationToken);
        return result.Ok();
    }
}
