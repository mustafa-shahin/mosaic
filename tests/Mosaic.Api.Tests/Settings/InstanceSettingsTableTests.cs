using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Domain.Settings;
using Mosaic.Persistence;
using Npgsql;

namespace Mosaic.Api.Tests.Settings;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class InstanceSettingsTableTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AClaim_ComesBackAsItWasSaved()
    {
        await Empty();
        await Save(InstanceSettings.Claim(Now));

        await using var scope = postgres.CreateScope();
        var settings = await Context(scope).InstanceSettings.SingleAsync(Token);

        Assert.Equal(InstanceSettings.SingleRowId, settings.Id);
        Assert.Equal(Now, settings.CreatedAt);
        Assert.Null(settings.UpdatedAt);
    }

    [Fact]
    public async Task ASecondClaim_IsRefusedByTheKey()
    {
        await Empty();
        await Save(InstanceSettings.Claim(Now));

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => Save(InstanceSettings.Claim(Now)));

        AssertViolates(refused.InnerException, PostgresErrorCodes.UniqueViolation, "pk_instance_settings");
    }

    [Fact]
    public async Task ARowWithAnotherId_IsRefusedByTheCheck()
    {
        await Empty();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => Execute(
            "INSERT INTO instance_settings (id, created_at) VALUES (2, now())"));

        AssertViolates(refused, PostgresErrorCodes.CheckViolation, "ck_instance_settings_single_row");
    }

    [Fact]
    public async Task AChangedRow_GetsItsUpdatedAt()
    {
        await Empty();
        await Save(InstanceSettings.Claim(Now));

        await using (var scope = postgres.CreateScope())
        {
            var context = Context(scope);
            var settings = await context.InstanceSettings.SingleAsync(Token);

            // InstanceSettings has no method that changes it, so the change goes through the change tracker
            context.Entry(settings).Property(row => row.CreatedAt).CurrentValue = Now.AddMinutes(1);

            await context.SaveChangesAsync(Token);
        }

        await using var reread = postgres.CreateScope();

        Assert.NotNull((await Context(reread).InstanceSettings.SingleAsync(Token)).UpdatedAt);
    }

    [Fact]
    public async Task ASaveThatChangedNothing_LeavesUpdatedAtEmpty()
    {
        await Empty();
        await Save(InstanceSettings.Claim(Now));

        await using (var scope = postgres.CreateScope())
        {
            var context = Context(scope);

            await context.InstanceSettings.SingleAsync(Token);
            await context.SaveChangesAsync(Token);
        }

        await using var reread = postgres.CreateScope();

        Assert.Null((await Context(reread).InstanceSettings.SingleAsync(Token)).UpdatedAt);
    }

    private async Task Save(InstanceSettings settings)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.InstanceSettings.Add(settings);

        await context.SaveChangesAsync(Token);
    }

    private async Task Execute(string sql)
    {
        await using var scope = postgres.CreateScope();

        await Context(scope).Database.ExecuteSqlRawAsync(sql, Token);
    }

    // the claim is one row for the whole database, so each test starts without it
    private Task Empty() => Execute("DELETE FROM instance_settings");

    private static void AssertViolates(Exception? refused, string sqlState, string constraint)
    {
        var error = Assert.IsType<PostgresException>(refused);

        Assert.Equal(sqlState, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
}
