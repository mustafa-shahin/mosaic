namespace Mosaic.Contracts.Auditing;

// one row of the audit list; the names are the copies the row took when the action happened
public sealed record AuditEntryResult
{
    public required Guid Id { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string Action { get; init; }
    public required bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? ActorName { get; init; }
    public string? ActorEmail { get; init; }
    public string? TargetType { get; init; }
    public string? TargetName { get; init; }
    public required string CorrelationId { get; init; }

    // where the request came from; only the owner receives it
    public string? IPAddress { get; init; }
}
