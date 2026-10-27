using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HomeMealPlanController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByWeek(
        [FromServices] IHomeMealPlanService service,
        [FromQuery] DateOnly? weekStartDate,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPlanByWeekAsync(weekStartDate, cancellationToken);
        return result.Ok();
    }

    [HttpPost("generate-week")]
    public async Task<IActionResult> GenerateWeek(
        [FromServices] IHomeMealPlanService service,
        [FromQuery] DateOnly? weekStartDate,
        [FromBody] ShoppingListGenerateModel? body,
        CancellationToken cancellationToken)
    {
        var targetDate = weekStartDate ?? body?.WeekStartDate;
        var result = await service.GenerateAutomaticPlanAsync(targetDate, cancellationToken);
        return result.Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(
        [FromServices] IHomeMealPlanService service,
        [FromBody] HomeMealPlanUpsertModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpsertPlanAsync(model, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("{uuid}")]
    public async Task<IActionResult> Delete(
        [FromServices] IHomeMealPlanService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeletePlanAsync(uuid, cancellationToken);
        return result.Ok();
    }
}

