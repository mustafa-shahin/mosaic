using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Identity;
using Mosaic.Application.Security;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class CheckSessionTests(PostgresFixture postgres)
{
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALiveToken_ReturnsItsSession()
    {
        var signedIn = await SignIn();

        var session = await Check(signedIn.Token);

        Assert.Equal(signedIn.SessionId, session?.Id);
    }

    [Fact]
    public async Task AWellFormedTokenNobodyHolds_IsRefused()
    {
        await SignIn();

        Assert.Null(await Check(SessionTokenFormat.Create().Token));
    }

    [Fact]
    public async Task OnceTheStampRotates_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        await ChangeUser(signedIn.UserId, user => user.EndAllSessions());

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task OnceThePersonIsDeactivated_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        await ChangeUser(signedIn.UserId, user => user.Deactivate());

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task OnceThePersonIsDeleted_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        await ChangeUser(signedIn.UserId, user => user.SoftDelete(_clock.Now, null));

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task OnceTheSessionIsRevoked_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        await ChangeSession(signedIn.SessionId, session => session.Revoke(_clock.Now));

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task ASessionLeftIdleForAWeek_IsRefused()
    {
        var signedIn = await SignIn();

        _clock.Now = _clock.Now.AddDays(7);

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task WithinFiveMinutes_LastUsedAtStaysWhereItWas()
    {
        var signedIn = await SignIn();
        var signedInAt = _clock.Now;

        _clock.Now = signedInAt.AddMinutes(4);

        await Check(signedIn.Token);

        Assert.Equal(signedInAt, (await FindSession(signedIn.SessionId)).LastUsedAt);
    }

    [Fact]
    public async Task AfterFiveMinutes_LastUsedAtIsSaved()
    {
        var signedIn = await SignIn();

        _clock.Now = _clock.Now.AddMinutes(5);

        await Check(signedIn.Token);

        Assert.Equal(_clock.Now, (await FindSession(signedIn.SessionId)).LastUsedAt);
    }

    // the person and the session are written straight to the database, the way sign-in leaves them
    private async Task<SignedIn> SignIn()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by the check", _clock.Now);
        var (token, hash) = SessionTokenFormat.Create();
        var session = SessionToken.Issue(user, hash, _clock.Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, session.Id, user.Id);
    }

    // a scope per check, so every check reads the database and not a copy an earlier one tracked
    private async Task<SessionToken?> Check(string token)
    {
        await using var scope = postgres.CreateScope();

        return await new CheckSession(Context(scope), _clock).HandleAsync(token, Token);
    }

    private async Task ChangeUser(Guid userId, Action<User> change)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Token);

        change(user);

        await context.SaveChangesAsync(Token);
    }

    private async Task ChangeSession(Guid sessionId, Action<SessionToken> change)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var session = await context.SessionTokens.SingleAsync(candidate => candidate.Id == sessionId, Token);

        change(session);

        await context.SaveChangesAsync(Token);
    }

    private async Task<SessionToken> FindSession(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private sealed record SignedIn(string Token, Guid SessionId, Guid UserId);
}
