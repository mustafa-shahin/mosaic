namespace Mosaic.Contracts.Identity;

public sealed record RegisterResult
{
    public RegistrationOutcome Outcome { get; init; }
}
