using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Mosaic.Application.Identity;

namespace Mosaic.Api.Identity;

internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    CheckSession checkSession)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "MosaicSession";

    // the registered claim name for a session id; ClaimTypes.Sid is a Windows security identifier
    public const string SessionIdClaim = "sid";

    private const string BearerPrefix = "Bearer ";

    // realm, because a Bearer challenge must carry at least one parameter
    private const string Challenge = "Bearer realm=\"mosaic\"";

    private const string InvalidToken = ", error=\"invalid_token\"";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // an endpoint open to everyone answers the same with or without a token, so /health never waits on the database
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return AuthenticateResult.NoResult();
        }

        string? header = Request.Headers.Authorization;

        // no Bearer token was presented, so the endpoint decides whether it needs a session
        if (header is null || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await checkSession.HandleAsync(header[BearerPrefix.Length..].Trim(), Context.RequestAborted);

        if (session is null)
        {
            return AuthenticateResult.Fail("The token does not open a live session.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(SessionIdClaim, session.Id.ToString()),
            ],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    // no body, because a bare 401 already means client.not_signed_in to the desktop and an envelope would be a second code for it
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var result = await HandleAuthenticateOnceSafeAsync();

        Response.StatusCode = StatusCodes.Status401Unauthorized;

        // no error_description, so an expired session and a revoked one look the same
        Response.Headers.WWWAuthenticate = result.Failure is null ? Challenge : Challenge + InvalidToken;
    }
}
