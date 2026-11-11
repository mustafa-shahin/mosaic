using System.Net;

namespace Mosaic.Domain.Auditing;

public sealed class AuditEntry
{
    public const int ActionMaxLength = 100;
    public const int ErrorCodeMaxLength = 100;
    public const int TargetTypeMaxLength = 50;
    public const int CorrelationIdMaxLength = 64;

    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Action { get; private set; } = null!;

    public AuditOutcome Outcome { get; private set; }

    public string? ErrorCode { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? ActorName { get; private set; }

    public string? ActorEmail { get; private set; }

    public string? TargetType { get; private set; }

    public Guid? TargetId { get; private set; }

    public string? TargetName { get; private set; }

    public IPAddress? IPAddress { get; private set; }

    public string CorrelationId { get; private set; } = null!;

    public static AuditEntry Succeeded(
        string action,
        AuditActor? actor,
        AuditTarget? target,
        AuditOrigin origin,
        DateTimeOffset now)
        => Create(action, AuditOutcome.Succeeded, null, actor, target, origin, now);

    public static AuditEntry Failed(
        string action,
        string errorCode,
        AuditActor? actor,
        AuditTarget? target,
        AuditOrigin origin,
        DateTimeOffset now)
        => Create(action, AuditOutcome.Failed, errorCode, actor, target, origin, now);

    private static AuditEntry Create(
        string action,
        AuditOutcome outcome,
        string? errorCode,
        AuditActor? actor,
        AuditTarget? target,
        AuditOrigin origin,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = now,
            Action = action,
            Outcome = outcome,
            ErrorCode = errorCode,
            ActorId = actor?.Id,
            ActorName = actor?.Name,
            ActorEmail = actor?.Email,
            TargetType = target?.Type,
            TargetId = target?.Id,
            TargetName = target?.Name,
            IPAddress = origin.IPAddress,
            CorrelationId = origin.CorrelationId,
        };
}

public enum AuditOutcome
{
    Succeeded,
    Failed,
}

public sealed record AuditActor(Guid Id, string Name, string Email);

public sealed record AuditTarget(string Type, Guid Id, string Name);

public sealed record AuditOrigin(IPAddress? IPAddress, string CorrelationId);
