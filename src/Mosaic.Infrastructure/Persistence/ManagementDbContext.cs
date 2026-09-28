using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;

namespace Mosaic.Infrastructure.Persistence;

public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options)
    : DbContext(options), IManagementDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();

    public DbSet<InstanceSettings> InstanceSettings => Set<InstanceSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ManagementDbContext).Assembly);
}
