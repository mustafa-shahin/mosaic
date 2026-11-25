using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity;
using Mosaic.Domain.Auditing;
using Mosaic.Persistence;
using Testcontainers.PostgreSql;

namespace Mosaic.Api.Tests;

// one throwaway Postgres for every test class in the collection, its schema built by the real migrations
public sealed class PostgresFixture : IAsyncLifetime
{
    // the image compose.yaml runs, so the tests and development use the same major version
    private const string Image = "postgres:18";

    public const string SetupToken = "setup-token-for-tests";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    // built in InitializeAsync, because the connection string exists only once the container runs
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _services = new ServiceCollection()
            .AddLogging()
            .AddPersistence(ConnectionString)
            .AddAccounts(SetupTokenPath)
            .BuildServiceProvider();

        await using var scope = CreateScope();

        await scope.ServiceProvider.GetRequiredService<ManagementDbContext>().Database.MigrateAsync();
    }

    public string ConnectionString => _container.GetConnectionString();

    public string SetupTokenPath { get; } = Path.Combine(Path.GetTempPath(), $"mosaic-setup-token-{Guid.CreateVersion7():N}");

    // a new scope means a new context, so reading in one sees the database, not a tracked copy
    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    // every class of the collection shares this database, so a test that resets starts like a fresh installation
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();

        await scope.ServiceProvider.GetRequiredService<ManagementDbContext>().Database
            .ExecuteSqlRawAsync("DELETE FROM users; DELETE FROM instance_settings; DELETE FROM audit_entries", cancellationToken);

        // a file that exists is read, not replaced, so every request can carry the known token
        await File.WriteAllTextAsync(SetupTokenPath, SetupToken, cancellationToken);
    }

    // after a reset the table holds only the rows of the test that reads it
    public async Task<List<AuditEntry>> AuditRowsAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ManagementDbContext>().AuditEntries.ToListAsync(cancellationToken);
    }

    // a context of its own with every statement recorded, so nothing tracked earlier can answer for the database
    public ManagementDbContext CreateLoggedContext(StatementLog log)
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>();

        DatabaseOptions.Configure(options, ConnectionString);
        options.AddInterceptors(log);

        return new ManagementDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        // xUnit calls this even when InitializeAsync failed, so the services may not exist
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();

        File.Delete(SetupTokenPath);
    }
}

[CollectionDefinition]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>;
