using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] IUserSettingsService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        return result.Ok();
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromServices] IUserSettingsService service,
        [FromBody] UserSettingsModel model,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(model, cancellationToken);
        return result.Ok();
    }
}
