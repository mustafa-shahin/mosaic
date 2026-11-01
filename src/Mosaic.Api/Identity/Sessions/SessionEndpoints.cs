using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Api.Authorization;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

// Microsoft.AspNetCore.Mvc has a SignInResult too, the one ControllerBase.SignIn returns
using SignInResult = Mosaic.Contracts.Identity.SignInResult;

namespace Mosaic.Api.Identity.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder auth)
    {
        auth.MapPost(AuthRoutes.SignIn, SignIn);
        auth.MapGet(AuthRoutes.Session, Session);
        auth.MapPost(AuthRoutes.SignOut, SignOut);
        auth.MapPost(AuthRoutes.SignOutEverywhere, SignOutEverywhere);

        return auth;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.SignIn)]
    public static async Task<ApiResponse<SignInResult>> SignIn(
        SignInRequest request,
        [FromServices] SignInUser signInUser,
        CancellationToken cancellationToken)
    {
        var result = await signInUser.HandleAsync(request, cancellationToken);

        return ApiResponse.Ok(result);
    }

    [AllowAnySignedIn]
    public static async Task<ApiResponse<SessionResult>> Session(
        ClaimsPrincipal user,
        [FromServices] GetSession getSession,
        CancellationToken cancellationToken)
    {
        // the authentication handler put the person's id here once the token passed CheckSession
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await getSession.HandleAsync(userId, cancellationToken);

        return ApiResponse.Ok(result);
    }

    [AllowAnySignedIn]
    public static async Task<ApiResponse> SignOut(
        ClaimsPrincipal user,
        [FromServices] SignOutUser signOutUser,
        CancellationToken cancellationToken)
    {
        // the id of this token's session row, so the person's other sessions stay signed in
        var sessionId = Guid.Parse(user.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim)!);

        await signOutUser.HandleAsync(sessionId, cancellationToken);

        return ApiResponse.Ok();
    }

    [AllowAnySignedIn]
    public static async Task<ApiResponse> SignOutEverywhere(
        ClaimsPrincipal user,
        [FromServices] SignOutEverywhere signOutEverywhere,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await signOutEverywhere.HandleAsync(userId, cancellationToken);

        return ApiResponse.Ok();
    }
}
