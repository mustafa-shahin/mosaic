using Mosaic.Domain.Auditing;
using System.Net;

namespace Mosaic.Domain.Tests.Auditing;

public sealed class AuditEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static AuditActor NewActor()
        => new(Guid.NewGuid(), "Alice", "alice@example.com");

    private static AuditTarget NewTarget()
        => new("user", Guid.NewGuid(), "Alice");

    private static AuditOrigin NewAuditOrigin()
        => new(IPAddress.Parse("203.0.113.5"), CorrelationId);

    private const string CorrelationId = "trace-1";

    [Fact]
    public void Succeeded_RecordsWhatItWasGiven()
    {
        var actor = NewActor();
        var target = NewTarget();
        var auditOrigin = NewAuditOrigin();
        var entry = AuditEntry.Succeeded("auth.sign_in.succeeded", actor, target, auditOrigin, Now);

        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("auth.sign_in.succeeded", entry.Action);
        Assert.Equal(AuditOutcome.Succeeded, entry.Outcome);
        Assert.Null(entry.ErrorCode);
        Assert.Equal(actor.Id, entry.ActorId);
        Assert.Equal(actor.Name, entry.ActorName);
        Assert.Equal(actor.Email, entry.ActorEmail);
        Assert.Equal(target.Type, entry.TargetType);
        Assert.Equal(target.Id, entry.TargetId);
        Assert.Equal(target.Name, entry.TargetName);
        Assert.Equal(auditOrigin.IPAddress, entry.IPAddress);
        Assert.Equal(auditOrigin.CorrelationId, entry.CorrelationId);
    }

    [Fact]
    public void Failed_RecordsTheOutcomeAndTheCode()
    {
        var actor = NewActor();
        var target = NewTarget();
        var auditOrigin = NewAuditOrigin();
        var entry = AuditEntry.Failed("auth.sign_in.failed", "error.invalid_credentials", actor, target, auditOrigin, Now);

        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("auth.sign_in.failed", entry.Action);
        Assert.Equal(AuditOutcome.Failed, entry.Outcome);
        Assert.Equal("error.invalid_credentials", entry.ErrorCode);
        Assert.Equal(actor.Id, entry.ActorId);
        Assert.Equal(actor.Name, entry.ActorName);
        Assert.Equal(actor.Email, entry.ActorEmail);
        Assert.Equal(target.Type, entry.TargetType);
        Assert.Equal(target.Id, entry.TargetId);
        Assert.Equal(target.Name, entry.TargetName);
        Assert.Equal(auditOrigin.IPAddress, entry.IPAddress);
        Assert.Equal(auditOrigin.CorrelationId, entry.CorrelationId);
    }

    [Fact]
    public void Failed_WithNobodyKnown_LeavesTheActorEmpty()
    {
        var target = NewTarget();
        var auditOrigin = NewAuditOrigin();
        var entry = AuditEntry.Failed("auth.sign_in.failed", "error.invalid_credentials", null, target, auditOrigin, Now);

        Assert.Null(entry.ActorId);
        Assert.Null(entry.ActorName);
        Assert.Null(entry.ActorEmail);
    }

    [Fact]
    public void Failed_ForAnUnknownAddress_LeavesTheTargetEmpty()
    {
        var actor = NewActor();
        var auditOrigin = NewAuditOrigin();
        var entry = AuditEntry.Failed("auth.sign_in.failed", "error.invalid_credentials", actor, null, auditOrigin, Now);

        Assert.Null(entry.TargetType);
        Assert.Null(entry.TargetId);
        Assert.Null(entry.TargetName);
    }

    [Fact]
    public void Succeeded_GivesEveryEntryItsOwnId()
    {
        var entry1 = AuditEntry.Succeeded("auth.sign_in.succeeded", NewActor(), NewTarget(), NewAuditOrigin(), Now);
        var entry2 = AuditEntry.Succeeded("auth.sign_in.succeeded", NewActor(), NewTarget(), NewAuditOrigin(), Now);

        Assert.NotEqual(entry1.Id, entry2.Id);
    }
}
