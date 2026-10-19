using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Api.Authorization;
using Mosaic.Api.Identity;
using Mosaic.Application.Identity;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

// Microsoft.AspNetCore.Mvc has a SignInResult too, the one ControllerBase.SignIn returns
using SignInResult = Mosaic.Contracts.Identity.SignInResult;

namespace Mosaic.Api.Controllers.Identity;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiVersions.RouteTemplate + "/" + AuthRoutes.Prefix)]
public sealed class AuthController : ControllerBase
{
    // only the address's global bucket: it costs no hash and takes no input to guess
    [HttpGet(AuthRoutes.Rules)]
    [AllowAnonymous]
    public async Task<ApiResponse<RulesResult>> Rules(
        [FromServices] GetRules getRules,
        CancellationToken cancellationToken)
    {
        var result = await getRules.HandleAsync(cancellationToken);

        return ApiResponse.Ok(result);
    }

    [HttpPost(AuthRoutes.Register)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Register)]
    public async Task<ActionResult<ApiResponse>> Register(
        RegisterRequest request,
        [FromServices] RegisterUser registerUser,
        CancellationToken cancellationToken)
    {
        await registerUser.HandleAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok());
    }

    [HttpPost(AuthRoutes.SignIn)]
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

    [HttpGet(AuthRoutes.Session)]
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

    [HttpPost(AuthRoutes.SignOut)]
    [AllowAnySignedIn]
    public async Task<ApiResponse> SignOut(
        [FromServices] SignOutUser signOutUser,
        CancellationToken cancellationToken)
    {
        // the id of this token's session row, so the person's other sessions stay signed in
        var sessionId = Guid.Parse(User.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim)!);

        await signOutUser.HandleAsync(sessionId, cancellationToken);

        return ApiResponse.Ok();
    }

    [HttpPost(AuthRoutes.SignOutEverywhere)]
    [AllowAnySignedIn]
    public async Task<ApiResponse> SignOutEverywhere(
        [FromServices] SignOutEverywhere signOutEverywhere,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await signOutEverywhere.HandleAsync(userId, cancellationToken);

        return ApiResponse.Ok();
    }
}
