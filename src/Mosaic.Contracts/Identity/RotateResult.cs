namespace Mosaic.Contracts.Identity;

// the new token, shown once like the one sign-in returns; no session, because the caller already knows who it is
public sealed record RotateResult
{
    public required string Token { get; init; }
}
