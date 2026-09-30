namespace Mosaic.Contracts.Identity;

// the token is shown once, here; the server keeps only its hash
public sealed record SignInResult
{
    public required string Token { get; init; }
    public required SessionResult Session { get; init; }
}
