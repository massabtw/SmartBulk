using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealTemplateController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] IMealTemplateService service,
        [FromBody] MealTemplateCreateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(model, cancellationToken);
        return result.Created(data => $"/api/MealTemplate/{data.Uuid}");
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] IMealTemplateService service,
        [FromQuery] string? mealType,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAllAsync(mealType, cancellationToken);
        return result.Ok();
    }

    [HttpGet("shake-adjustment")]
    public async Task<IActionResult> GetShakeAdjustment(
        [FromServices] IShakeAdjustmentService service,
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? templateUuid,
        CancellationToken cancellationToken)
    {
        var result = await service.GetShakeAdjustmentAsync(date, templateUuid, cancellationToken);
        return result.Ok();
    }

    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetByUuid(
        [FromServices] IMealTemplateService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByUuidAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpPut("{uuid}")]
    public async Task<IActionResult> Update(
        [FromServices] IMealTemplateService service,
        [FromRoute] Guid uuid,
        [FromBody] MealTemplateUpdateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(uuid, model, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("{uuid}")]
    public async Task<IActionResult> Delete(
        [FromServices] IMealTemplateService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(uuid, cancellationToken);
        return result.Ok();
    }
}

