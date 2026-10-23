using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Api.Extensions;
using SmartBulk.Application.Interfaces;

namespace SmartBulk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    [HttpGet("/health")]
    public async Task<IActionResult> Get([FromServices] IHealthService service)
    {
        var result = await service.CheckHealthAsync();
        return result.Ok();
    }
}
