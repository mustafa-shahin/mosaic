using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Auditing;

namespace Mosaic.Persistence.Configurations.Auditing;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(auditEntry => auditEntry.Id);

        // pages sort by time, then id; a filter's index starts with the column it compares, then the time
        builder.HasIndex(auditEntry => new { auditEntry.OccurredAt, auditEntry.Id });
        builder.HasIndex(auditEntry => new { auditEntry.Outcome, auditEntry.OccurredAt });
        builder.HasIndex(auditEntry => new { auditEntry.Action, auditEntry.OccurredAt });
        builder.HasIndex(auditEntry => new { auditEntry.ActorId, auditEntry.OccurredAt });
        builder.HasIndex(auditEntry => new { auditEntry.TargetType, auditEntry.TargetId, auditEntry.OccurredAt });

        builder.Property(auditEntry => auditEntry.Action).HasMaxLength(AuditEntry.ActionMaxLength);
        builder.Property(auditEntry => auditEntry.ErrorCode).HasMaxLength(AuditEntry.ErrorCodeMaxLength);
        builder.Property(auditEntry => auditEntry.TargetType).HasMaxLength(AuditEntry.TargetTypeMaxLength);
        builder.Property(auditEntry => auditEntry.CorrelationId).HasMaxLength(AuditEntry.CorrelationIdMaxLength);

        builder.Property(auditEntry => auditEntry.Outcome).HasConversion<string>();

        var outcomes = string.Join(", ", Enum.GetNames<AuditOutcome>().Select(name => $"'{name}'"));

        builder.ToTable(table => table.HasCheckConstraint("ck_audit_entries_outcome", $"outcome IN ({outcomes})"));
    }
}
