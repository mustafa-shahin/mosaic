namespace Mosaic.Contracts.Identity;

// a missing field has to reach the validator, required would make the JSON reader reject it first
public sealed record RegisterRequest
{
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Password { get; init; }
    public string? SetupToken { get; init; }
}
