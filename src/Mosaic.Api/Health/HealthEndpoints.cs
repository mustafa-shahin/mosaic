using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mosaic.Contracts;
using Mosaic.Contracts.Health;

namespace Mosaic.Api.Health;

public static class HealthEndpoints
{
    private const string ServiceName = "Mosaic.Api";

    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(HealthRoutes.Health, Get);

        return endpoints;
    }

    [AllowAnonymous]
    public static ApiResponse<HealthResult> Get([FromServices] TimeProvider clock) =>
        ApiResponse.Ok(new HealthResult
        {
            Service = ServiceName,
            ServerTimeUtc = clock.GetUtcNow(),
            ApiContract = ApiContract.Version
        });
}
