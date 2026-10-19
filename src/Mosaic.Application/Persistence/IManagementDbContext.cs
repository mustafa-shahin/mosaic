using Microsoft.EntityFrameworkCore;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;

namespace Mosaic.Application.Persistence;

public interface IManagementDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<SessionToken> SessionTokens { get; }

    DbSet<InstanceSettings> InstanceSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
