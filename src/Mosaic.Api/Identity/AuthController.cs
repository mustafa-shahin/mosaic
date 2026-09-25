using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Mosaic.Application.Identity;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Identity;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResult>>> Register(
        RegisterRequest request,
        [FromServices] RegisterUser registerUser,
        CancellationToken cancellationToken)
    {
        var result = await registerUser.HandleAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }
}
