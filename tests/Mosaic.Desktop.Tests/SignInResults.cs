using Mosaic.Contracts.Identity;

namespace Mosaic.Desktop.Tests;

// A sign-in answer as the desktop holds it, for the tests that need a session without a server.
internal static class SignInResults
{
    public static SignInResult Ada(string token = "mosaic_ada-test-token") => new()
    {
        Token = token,
        Session = new SessionResult { UserId = Guid.CreateVersion7(), Email = "ada@example.com", FullName = "Ada Lovelace" }
    };
}
