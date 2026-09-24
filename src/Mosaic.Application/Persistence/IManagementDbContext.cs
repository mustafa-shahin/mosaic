using Microsoft.EntityFrameworkCore;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;

namespace Mosaic.Application.Persistence;

public interface IManagementDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<InstanceSettings> InstanceSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
