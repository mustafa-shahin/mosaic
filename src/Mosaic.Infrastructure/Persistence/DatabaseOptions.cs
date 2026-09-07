using Microsoft.EntityFrameworkCore;

namespace Mosaic.Infrastructure.Persistence;

internal static class DatabaseOptions
{
    // Shared by AddInfrastructure and ManagementDbContextFactory so the application and dotnet ef build
    // the same model. The migrations were generated with snake_case names; without the convention every
    // query would ask for tables that do not exist.
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
