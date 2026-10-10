using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;
using Npgsql;

namespace Mosaic.Integration.Tests.Identity;

// the migration that adds rotation edits rows by hand, so it is run here on rows that existed before it
[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SessionRotationMigrationTests(PostgresFixture postgres)
{
    private const string BeforeRotation = "20261004071025_AddSessionRevokedAt";

    private static readonly DateTimeOffset SignedInAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = SignedInAt.AddDays(30);
    private static readonly DateTimeOffset SignedOutAt = SignedInAt.AddHours(2);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachExistingSession_StartsItsOwnFamily_AndASignedOutOneEndsWhenItWasSignedOut()
    {
        // a database of its own, so it can stop at the migration before rotation
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = $"rotation_{Guid.CreateVersion7():N}" }.ConnectionString;
        var builder = new DbContextOptionsBuilder<ManagementDbContext>();

        DatabaseOptions.Configure(builder, connectionString);

        await using var context = new ManagementDbContext(builder.Options);

        await context.GetService<IMigrator>().MigrateAsync(BeforeRotation, Token);

        var user = User.CreateOwner("ada@mosaic.test", "Ada", "Lovelace", "unused by the migration", SignedInAt);
        var live = Guid.CreateVersion7();
        var signedOut = Guid.CreateVersion7();

        context.Users.Add(user);

        await context.SaveChangesAsync(Token);

        // written with the columns the table had then, because the entity already has the new ones
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO session_tokens (id, user_id, token_hash, security_stamp, last_used_at, expires_at, revoked_at, created_at)
            VALUES ({live}, {user.Id}, {RandomNumberGenerator.GetBytes(32)}, {user.SecurityStamp}, {SignedInAt}, {ExpiresAt}, NULL, {SignedInAt}),
                   ({signedOut}, {user.Id}, {RandomNumberGenerator.GetBytes(32)}, {user.SecurityStamp}, {SignedInAt}, {ExpiresAt}, {SignedOutAt}, {SignedInAt})
            """, Token);

        await context.Database.MigrateAsync(Token);

        var rows = await context.SessionTokens.AsNoTracking().ToDictionaryAsync(row => row.Id, Token);

        Assert.Equal(live, rows[live].FamilyId);
        Assert.Equal(signedOut, rows[signedOut].FamilyId);
        Assert.Equal(ExpiresAt, rows[live].ExpiresAt);
        Assert.Equal(SignedOutAt, rows[signedOut].ExpiresAt);
        Assert.Null(rows[live].ReplacesId);
    }
}
