using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

    // the rows stay, and the family's first row carries the time, so every later check refuses the family
    [Fact]
    public async Task SigningOut_EndsTheFamilyThroughItsFirstRow_AndKeepsTheRows()
    {
        var signedIn = await SignIn();
        var next = await Rotate(signedIn);

        _clock.Now = _clock.Now.AddMinutes(1);

        await SignOut(next.SessionId);

        Assert.Equal(_clock.Now, (await FindSession(signedIn.SessionId)).ExpiresAt);
        Assert.True(await Exists(next.SessionId));
    }

    [Fact]
    public async Task SigningOut_AlsoEndsAReplacedRowInsideItsGrace()
    {
        var signedIn = await SignIn();
        var next = await Rotate(signedIn);

        _clock.Now = _clock.Now.AddSeconds(10);

        await SignOut(next.SessionId);

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task SigningOut_LeavesThePersonsOtherSessionAsItWas()
    {
        var signedIn = await SignIn();
        var other = await SignInAgain(signedIn);
        var expiresAt = (await FindSession(other.SessionId)).ExpiresAt;

        await SignOut(signedIn.SessionId);

        Assert.Equal(expiresAt, (await FindSession(other.SessionId)).ExpiresAt);
    }

    // the person and the session are written straight to the database, the way sign-in leaves them
    private async Task<SignedIn> SignIn()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by sign-out", _clock.Now);
        var (token, hash) = SessionTokenFormat.Create();
        var session = SessionToken.Issue(user, hash, _clock.Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, session.Id, user.Id);
    }

    // a second sign-in of the same person, which starts a family of its own
    private async Task<SignedIn> SignInAgain(SignedIn signedIn)
    {
        var (token, hash) = SessionTokenFormat.Create();

        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var user = await context.Users.SingleAsync(candidate => candidate.Id == signedIn.UserId, Token);
        var session = SessionToken.Issue(user, hash, _clock.Now);

        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, session.Id, user.Id);
    }

    private async Task<SignedIn> Rotate(SignedIn current)
    {
        var (token, hash) = SessionTokenFormat.Create();

        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var session = await context.SessionTokens.SingleAsync(candidate => candidate.Id == current.SessionId, Token);
        var next = session.Rotate(hash, _clock.Now);

        context.SessionTokens.Add(next);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, next.Id, current.UserId);
    }

    private async Task SignOut(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        await new SignOutUser(Context(scope), _clock).HandleAsync(sessionId, Token);
    }

    private async Task<SessionToken?> Check(string token)
    {
        await using var scope = postgres.CreateScope();

        return await new CheckSession(Context(scope), _clock, NullLogger<CheckSession>.Instance).HandleAsync(token, Token);
    }

    private async Task<SessionToken> FindSession(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private async Task<bool> Exists(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.AnyAsync(session => session.Id == sessionId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private sealed record SignedIn(string Token, Guid SessionId, Guid UserId);
}
