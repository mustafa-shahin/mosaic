using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Infrastructure;
using Mosaic.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Mosaic.Integration.Tests;

// one throwaway Postgres for every test class in the collection, its schema built by the real migrations
public sealed class PostgresFixture : IAsyncLifetime
{
    // the image compose.yaml runs, so the tests and development use the same major version
    private const string Image = "postgres:18";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    // built in InitializeAsync, because the connection string exists only once the container runs
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _services = new ServiceCollection()
            .AddInfrastructure(ConnectionString)
            .BuildServiceProvider();

        await using var scope = CreateScope();

        await scope.ServiceProvider.GetRequiredService<ManagementDbContext>().Database.MigrateAsync();
    }

    public string ConnectionString => _container.GetConnectionString();

    // a new scope means a new context, so reading in one sees the database, not a tracked copy
    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    // the claim is one row for the whole database, so a registration test starts without it and without users
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();

        await scope.ServiceProvider.GetRequiredService<ManagementDbContext>().Database
            .ExecuteSqlRawAsync("DELETE FROM users; DELETE FROM instance_settings", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // xUnit calls this even when InitializeAsync failed, so the services may not exist
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>;
