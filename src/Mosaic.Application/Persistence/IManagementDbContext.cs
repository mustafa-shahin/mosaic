using Microsoft.EntityFrameworkCore;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Persistence;

public interface IManagementDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
