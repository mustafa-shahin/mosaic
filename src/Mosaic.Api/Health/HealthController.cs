using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Mosaic.Contracts;
using Mosaic.Contracts.Health;

namespace Mosaic.Api.Health;

[ApiController]
// without it the versioning counts this as a v1 controller and /health answers 404
[ApiVersionNeutral]
[Route("health")]
public sealed class HealthController(TimeProvider clock) : ControllerBase
{
    private const string ServiceName = "Mosaic.Api";

    [HttpGet]
    public ApiResponse<HealthResult> Get() =>
        ApiResponse.Ok(new HealthResult
        {
            Service = ServiceName,
            ServerTimeUtc = clock.GetUtcNow(),
            ApiContract = ApiContract.Version
        });
}