namespace Mosaic.Contracts.Auditing;

// one account the person filter can offer, with the email that tells two people of the same name apart
public sealed record AuditPersonResult
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}
