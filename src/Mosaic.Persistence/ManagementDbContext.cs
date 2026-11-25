using Microsoft.EntityFrameworkCore;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;

namespace Mosaic.Persistence;

public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();

    public DbSet<InstanceSettings> InstanceSettings => Set<InstanceSettings>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // one commit for the change and its row, so no change is saved without its record
    public Task<int> SaveChangesAsync(AuditEntry auditEntry, CancellationToken cancellationToken)
    {
        AuditEntries.Add(auditEntry);

        return SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ManagementDbContext).Assembly);
}
