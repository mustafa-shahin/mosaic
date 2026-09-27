namespace Mosaic.Contracts.Identity;

// no validator: a field error would tell a stranger which half of the pair was wrong
public sealed record SignInRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}
