using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShoppingListController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByWeek(
        [FromServices] IShoppingListService service,
        [FromQuery] DateOnly? weekStartDate,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByWeekAsync(weekStartDate, cancellationToken);
        return result.Ok();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateFromPlan(
        [FromServices] IShoppingListService service,
        [FromBody] ShoppingListGenerateModel? model,
        CancellationToken cancellationToken)
    {
        var result = await service.GenerateFromPlanAsync(model?.WeekStartDate, cancellationToken);
        return result.Ok();
    }

    [HttpPut("items/{itemUuid}/toggle")]
    public async Task<IActionResult> TogglePurchased(
        [FromServices] IShoppingListService service,
        [FromRoute] Guid itemUuid,
        [FromBody] ShoppingListItemToggleModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.TogglePurchasedAsync(itemUuid, model.IsPurchased, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("{uuid}")]
    public async Task<IActionResult> Delete(
        [FromServices] IShoppingListService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(uuid, cancellationToken);
        return result.Ok();
    }
}

