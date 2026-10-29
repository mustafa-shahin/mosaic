using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Identity.Registration;

public static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistration(this IEndpointRouteBuilder auth)
    {
        auth.MapGet(AuthRoutes.Rules, Rules);
        auth.MapPost(AuthRoutes.Register, Register);

        return auth;
    }

    // only the address's global bucket: it costs no hash and takes no input to guess
    [AllowAnonymous]
    public static async Task<ApiResponse<RulesResult>> Rules(
        [FromServices] GetRules getRules,
        CancellationToken cancellationToken)
    {
        var result = await getRules.HandleAsync(cancellationToken);

        return ApiResponse.Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Register)]
    public static async Task<JsonHttpResult<ApiResponse>> Register(
        RegisterRequest request,
        [FromServices] RegisterUser registerUser,
        CancellationToken cancellationToken)
    {
        await registerUser.HandleAsync(request, cancellationToken);

        return TypedResults.Json(ApiResponse.Ok(), statusCode: StatusCodes.Status201Created);
    }
}
