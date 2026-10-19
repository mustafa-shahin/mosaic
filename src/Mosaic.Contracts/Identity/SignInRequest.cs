namespace Mosaic.Contracts.Identity;

// no validator: a password rule raised later would refuse everyone whose password no longer meets it
public sealed record SignInRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}
