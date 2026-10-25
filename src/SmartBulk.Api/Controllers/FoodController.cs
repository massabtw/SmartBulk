using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites(
        [FromServices] IFoodService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetFavoritesAsync(cancellationToken);
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
        return result.Created(data => $"/api/Food/{data.Uuid}");
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

    [HttpPut("{uuid}/favorite")]
    public async Task<IActionResult> SetFavorite(
        [FromServices] IFoodService service,
        [FromRoute] Guid uuid,
        [FromBody] FoodFavoriteUpdateModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.SetFavoriteAsync(uuid, model.IsFavorite, cancellationToken);
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
