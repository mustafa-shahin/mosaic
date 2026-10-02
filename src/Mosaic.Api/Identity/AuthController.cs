using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Api.Authorization;
using Mosaic.Application.Identity;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

// Microsoft.AspNetCore.Mvc has a SignInResult too, the one ControllerBase.SignIn returns
using SignInResult = Mosaic.Contracts.Identity.SignInResult;

namespace Mosaic.Api.Identity;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Register)]
    public async Task<ActionResult<ApiResponse<RegisterResult>>> Register(
        RegisterRequest request,
        [FromServices] RegisterUser registerUser,
        CancellationToken cancellationToken)
    {
        var result = await registerUser.HandleAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<ApiResponse<SignInResult>> SignIn(
        SignInRequest request,
        [FromServices] SignInUser signInUser,
        CancellationToken cancellationToken)
    {
        var result = await signInUser.HandleAsync(request, cancellationToken);

        return ApiResponse.Ok(result);
    }

    [HttpGet("session")]
    [AllowAnySignedIn]
    public async Task<ApiResponse<SessionResult>> Session(
        [FromServices] GetSession getSession,
        CancellationToken cancellationToken)
    {
        // the authentication handler put the person's id here once the token passed CheckSession
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await getSession.HandleAsync(userId, cancellationToken);

        return ApiResponse.Ok(result);
    }
}
