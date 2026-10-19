namespace Mosaic.Contracts.Identity;

public sealed record RulesResult
{
    public required RegistrationState Registration { get; init; }
    public required PasswordRule Password { get; init; }
}
