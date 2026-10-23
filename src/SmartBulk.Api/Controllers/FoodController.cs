using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromServices] IFoodService service,
        [FromQuery] FoodQueryModel query,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(query, cancellationToken);
        return result.Ok();
    }

    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetByUuid(
        [FromServices] IFoodService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByUuidAsync(uuid, cancellationToken);
        return result.Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] IFoodService service,
        [FromBody] FoodModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateCustomAsync(model, cancellationToken);
        if (result.Success && result.Data != null)
        {
            return result.Created($"/api/Food/{result.Data.Uuid}");
        }

        return result.Created();
    }

    [HttpPut("{uuid}")]
    public async Task<IActionResult> Update(
        [FromServices] IFoodService service,
        [FromRoute] Guid uuid,
        [FromBody] FoodModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateCustomAsync(uuid, model, cancellationToken);
        return result.Ok();
    }

    [HttpDelete("{uuid}")]
    public async Task<IActionResult> Delete(
        [FromServices] IFoodService service,
        [FromRoute] Guid uuid,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteCustomAsync(uuid, cancellationToken);
        return result.Ok();
    }
}
