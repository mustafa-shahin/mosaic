using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Identity;
using Mosaic.Application.Security;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignOutUserTests(PostgresFixture postgres)
{
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the row stays with the time on it, so every later check finds it and refuses it
    [Fact]
    public async Task SigningOut_KeepsTheRowAndRecordsTheTime()
    {
        var sessions = await SignInTwice();

        _clock.Now = _clock.Now.AddMinutes(1);

        await SignOut(sessions.First);

        Assert.Equal(_clock.Now, (await FindSession(sessions.First)).RevokedAt);
    }

    [Fact]
    public async Task SigningOut_LeavesThePersonsOtherSessionAsItWas()
    {
        var sessions = await SignInTwice();

        await SignOut(sessions.First);

        Assert.Null((await FindSession(sessions.Second)).RevokedAt);
    }

    // one person with two sessions, written straight to the database the way two sign-ins leave them
    private async Task<Sessions> SignInTwice()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by sign-out", _clock.Now);
        var first = SessionToken.Issue(user, SessionTokenFormat.Create().Hash, _clock.Now);
        var second = SessionToken.Issue(user, SessionTokenFormat.Create().Hash, _clock.Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.AddRange(first, second);

        await context.SaveChangesAsync(Token);

        return new Sessions(first.Id, second.Id);
    }

    private async Task SignOut(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        await new SignOutUser(Context(scope), _clock).HandleAsync(sessionId, Token);
    }

    private async Task<SessionToken> FindSession(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private sealed record Sessions(Guid First, Guid Second);
}
