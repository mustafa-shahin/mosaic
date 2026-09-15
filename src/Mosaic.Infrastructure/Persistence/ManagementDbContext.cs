using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Domain.Identity;

namespace Mosaic.Infrastructure.Persistence;

public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options)
    : DbContext(options), IManagementDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ManagementDbContext).Assembly);
}
