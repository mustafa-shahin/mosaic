using Microsoft.EntityFrameworkCore;

namespace Mosaic.Infrastructure.Persistence;

internal static class DatabaseOptions
{
    // Shared with ManagementDbContextFactory, so dotnet ef builds the same snake_case model the API queries.
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
