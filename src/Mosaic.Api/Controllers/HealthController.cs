using Microsoft.AspNetCore.Mvc;
using Mosaic.Contracts;

namespace Mosaic.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(TimeProvider clock) : ControllerBase
{
    private const string ServiceName = "Mosaic.Api";

    [HttpGet]
    public ApiResponse<HealthResult> Get() =>
        ApiResponse.Ok(new HealthResult
        {
            Service = ServiceName,
            ServerTimeUtc = clock.GetUtcNow()
        });
}