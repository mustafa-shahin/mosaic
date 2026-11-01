using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Domain.Common;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests;

public sealed class ModelDriftTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ManagementDbContext _context;

    // Built through AddPersistence for the application's naming convention, and neither test opens a connection.
    public ModelDriftTests()
    {
        _provider = new ServiceCollection()
            .AddPersistence(ApiFactory.ConnectionString)
            .BuildServiceProvider();

        _context = _provider.GetRequiredService<ManagementDbContext>();
    }

    // The provider owns the context and disposes it.
    public void Dispose() => _provider.Dispose();

    [Fact]
    public void TheModelMatchesTheLatestMigration()
    {
        Assert.False(
            _context.Database.HasPendingModelChanges(),
            "The model has changed since the last migration. Run dotnet ef migrations add.");
    }

    // Without a filter, deleted rows come back from every ordinary query.
    [Fact]
    public void EverySoftDeletableTypeFiltersDeletedRowsOut()
    {
        var softDeletable = _context.Model.GetEntityTypes()
            .Where(entity => entity.ClrType.IsAssignableTo(typeof(SoftDeletableEntity)))
            .ToList();

        Assert.NotEmpty(softDeletable);

        var unfiltered = softDeletable
            .Where(entity => entity.GetDeclaredQueryFilters().Count == 0)
            .Select(entity => entity.ClrType.Name)
            .Order()
            .ToList();

        Assert.True(unfiltered.Count == 0, $"No query filter on: {string.Join(", ", unfiltered)}");
    }
}
