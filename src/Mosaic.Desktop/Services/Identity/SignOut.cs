using Mosaic.Contracts;

namespace Mosaic.Desktop.Services.Identity;

// Both ways out of a session. Owned by App, because the shell and the Settings screen both use it.
public sealed class SignOut(AuthApi auth, CurrentSession session)
{
    // the window changes before the server is asked, and a row the server never deletes still ends at its expiry
    public Task HereAsync()
    {
        if (session.Current is not { } current)
        {
            return Task.CompletedTask;
        }

        session.End(current, SessionEndReason.SignedOut);

        return auth.SignOutAsync(current, CancellationToken.None);
    }

    // waits for the server, because a stolen copy of the token keeps working until the server has changed the stamp
    public async Task<ApiResponse> EverywhereAsync(CancellationToken cancellationToken)
    {
        if (session.Current is not { } current)
        {
            return ApiResponse.Fail(new ApiError { Code = DesktopErrorCodes.NotSignedIn });
        }

        var answer = await auth.SignOutEverywhereAsync(current, cancellationToken);

        if (answer.Success)
        {
            session.End(current, SessionEndReason.SignedOutEverywhere);
        }

        return answer;
    }
}
