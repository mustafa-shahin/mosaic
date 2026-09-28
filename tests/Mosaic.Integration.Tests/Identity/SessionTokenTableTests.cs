using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;
using Npgsql;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SessionTokenTableTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASession_ComesBackAsItWasSaved()
    {
        var user = await SaveUser();
        var hash = RandomNumberGenerator.GetBytes(32);

        await Save(SessionToken.Issue(user, hash, Now));

        await using var scope = postgres.CreateScope();
        var session = await Context(scope).SessionTokens.SingleAsync(row => row.UserId == user.Id, Token);

        Assert.Equal(hash, session.TokenHash);
        Assert.Equal(user.SecurityStamp, session.SecurityStamp);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now, session.LastUsedAt);
        Assert.Equal(Now.AddDays(30), session.ExpiresAt);
        Assert.Null(session.UpdatedAt);
    }

    [Fact]
    public async Task ASecondSessionWithTheSameHash_IsRefusedByTheIndex()
    {
        var user = await SaveUser();
        var hash = RandomNumberGenerator.GetBytes(32);

        await Save(SessionToken.Issue(user, hash, Now));

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => Save(SessionToken.Issue(user, hash, Now)));

        var error = Assert.IsType<PostgresException>(refused.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ix_session_tokens_token_hash", error.ConstraintName);
    }

    [Fact]
    public async Task DeletingAUser_DeletesTheirSessions()
    {
        var user = await SaveUser();

        await Save(SessionToken.Issue(user, RandomNumberGenerator.GetBytes(32), Now));

        await using (var scope = postgres.CreateScope())
        {
            await Context(scope).Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {user.Id}", Token);
        }

        await using var reread = postgres.CreateScope();

        Assert.False(await Context(reread).SessionTokens.AnyAsync(row => row.UserId == user.Id, Token));
    }

    private async Task<User> SaveUser()
    {
        // the database lives for the whole run, so every test needs an address no other test used
        var user = User.Register($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "hash", null, Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);

        await context.SaveChangesAsync(Token);

        return user;
    }

    private async Task Save(SessionToken session)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
}
