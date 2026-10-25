using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CafeteriaMenuController : ControllerBase
{
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(
        [FromServices] ICafeteriaMenuService service,
        [FromBody] CafeteriaMenuIngestModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.IngestMenuAsync(model, cancellationToken);
        return result.Created(data => $"/api/CafeteriaMenu/{data.Uuid}");
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate(
        [FromServices] ICafeteriaMenuService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMenuByDateAsync(date, cancellationToken);
        return result.Ok();
    }

    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetByUuid(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMenuByUuidAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpPut("items/{itemUuid}")]
    public async Task<IActionResult> UpdateItem(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid itemUuid,
        [FromBody] CafeteriaMealItemUpdateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateItemAsync(itemUuid, model, cancellationToken);
        return result.Ok();
    }

    [HttpPost("meals/{mealUuid}/items")]
    public async Task<IActionResult> AddItem(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid mealUuid,
        [FromBody] CafeteriaMealItemCreateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.AddItemAsync(mealUuid, model, cancellationToken);
        return result.Created();
    }

    [HttpDelete("items/{itemUuid}")]
    public async Task<IActionResult> DeleteItem(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid itemUuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteItemAsync(itemUuid, cancellationToken);
        return result.Ok();
    }

    [HttpPost("{uuid}/confirm")]
    public async Task<IActionResult> Confirm(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmMenuAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpPost("{uuid}/reopen")]
    public async Task<IActionResult> Reopen(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.ReopenMenuAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpPut("meals/{mealUuid}/profile")]
    public async Task<IActionResult> UpdateMealPlateProfile(
        [FromServices] ICafeteriaMenuService service,
        [FromRoute] Guid mealUuid,
        [FromBody] CafeteriaMealPlateProfileUpdateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateMealPlateProfileAsync(mealUuid, model, cancellationToken);
        return result.Ok();
    }

    [HttpPut("meals/profile")]
    public async Task<IActionResult> UpdateMealPlateProfileByDate(
        [FromServices] ICafeteriaMenuService service,
        [FromBody] CafeteriaMealPlateProfileByDateUpdateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateMealPlateProfileByDateAsync(model, cancellationToken);
        return result.Ok();
    }

    [HttpGet("day-override")]
    public async Task<IActionResult> GetDayOverride(
        [FromServices] ICafeteriaMenuService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDayOverrideAsync(date, cancellationToken);
        return result.Ok();
    }

    [HttpPost("day-override")]
    public async Task<IActionResult> SetDayOverride(
        [FromServices] ICafeteriaMenuService service,
        [FromBody] CafeteriaDayOverrideUpsertModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.SetDayOverrideAsync(model, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("day-override")]
    public async Task<IActionResult> DeleteDayOverride(
        [FromServices] ICafeteriaMenuService service,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteDayOverrideAsync(date, cancellationToken);
        return result.Ok();
    }

    [HttpGet("daily-guaranteed")]
    public async Task<IActionResult> GetDailyGuaranteed(
        [FromServices] ICafeteriaMenuService service,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDailyGuaranteedAsync(date, cancellationToken);
        return result.Ok();
    }
}
