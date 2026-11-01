using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mosaic.Persistence;
using Npgsql;

namespace Mosaic.Api.Tests.Identity;

// the migration that adds the stamp is edited by hand, so it is run here on people who existed before it
[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SecurityStampMigrationTests(PostgresFixture postgres)
{
    private const string BeforeTheStamp = "20260907202314_InitialCreate";

    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryoneWhoExistedBefore_GetsAStampOfTheirOwn_AndTheColumnKeepsNoDefault()
    {
        // a database of its own, so it can stop at the migration before the stamp
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = $"stamp_{Guid.CreateVersion7():N}" }.ConnectionString;
        var builder = new DbContextOptionsBuilder<ManagementDbContext>();

        DatabaseOptions.Configure(builder, connectionString);

        await using var context = new ManagementDbContext(builder.Options);

        await context.GetService<IMigrator>().MigrateAsync(BeforeTheStamp, Token);

        // written with the columns the table had then, because the entity already has the stamp
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, is_owner, is_active, totp_enabled, failed_login_attempts, created_at, is_deleted)
            VALUES ({Guid.CreateVersion7()}, 'ada@mosaic.test', 'Ada', 'Lovelace', 'hash', TRUE, TRUE, FALSE, 0, {Now}, FALSE),
                   ({Guid.CreateVersion7()}, 'grace@mosaic.test', 'Grace', 'Hopper', 'hash', FALSE, TRUE, FALSE, 0, {Now}, FALSE)
            """, Token);

        await context.Database.MigrateAsync(Token);

        var stamps = await context.Users.AsNoTracking().Select(user => user.SecurityStamp).ToListAsync(Token);
        var columnDefault = await context.Database
            .SqlQuery<string?>($"""SELECT column_default AS "Value" FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'security_stamp'""")
            .SingleAsync(Token);

        Assert.Equal(2, stamps.Count);
        Assert.DoesNotContain(Guid.Empty, stamps);
        Assert.NotEqual(stamps[0], stamps[1]);
        Assert.Null(columnDefault);
    }
}
