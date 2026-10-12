using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private readonly RecordingLogger<CheckSession> _log = new();

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
    public async Task OnceItsFamilyHasEnded_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        await EndFamily(signedIn.SessionId);

        Assert.Null(await Check(signedIn.Token));
    }

    [Fact]
    public async Task AReplacedTokenInsideItsGrace_IsStillAccepted()
    {
        var signedIn = await SignIn();

        await Rotate(signedIn);

        _clock.Now = _clock.Now.AddSeconds(29);

        Assert.NotNull(await Check(signedIn.Token));
    }

    // the family ends through its first row, so the newest token of the sign-in stops as well
    [Fact]
    public async Task AReplacedTokenAfterItsGrace_EndsItsFamily()
    {
        var signedIn = await SignIn();
        var next = await Rotate(signedIn);

        _clock.Now = _clock.Now.AddSeconds(30);

        Assert.Null(await Check(signedIn.Token));
        Assert.Null(await Check(next.Token));

        var first = await FindSession(signedIn.SessionId);

        Assert.Equal(_clock.Now, first.ExpiresAt);
        Assert.Equal(_clock.Now, first.UpdatedAt);
    }

    [Fact]
    public async Task AReplacedLaterRow_EndsTheWholeFamily()
    {
        var signedIn = await SignIn();
        var second = await Rotate(signedIn);
        var third = await Rotate(second);

        _clock.Now = _clock.Now.AddSeconds(30);

        Assert.Null(await Check(second.Token));
        Assert.Null(await Check(third.Token));
    }

    [Fact]
    public async Task AReplacedTokenThreeDaysLater_EndsItsFamily_WhileThePersonsOtherSessionKeepsWorking()
    {
        var signedIn = await SignIn();
        var other = await SignInAgain(signedIn);
        var next = await Rotate(signedIn);

        _clock.Now = _clock.Now.AddDays(3);

        Assert.Null(await Check(signedIn.Token));
        Assert.Null(await Check(next.Token));
        Assert.NotNull(await Check(other.Token));
    }

    // the thief keeps the new token in use while the replaced one is never used again
    [Fact]
    public async Task AReplacedTokenAfterAWeekWithoutRequests_StillEndsItsFamily()
    {
        var signedIn = await SignIn();
        var next = await Rotate(signedIn);

        _clock.Now = _clock.Now.AddDays(4);

        Assert.NotNull(await Check(next.Token));

        _clock.Now = _clock.Now.AddDays(4);

        Assert.Null(await Check(signedIn.Token));
        Assert.Null(await Check(next.Token));
    }

    [Fact]
    public async Task AReplay_WritesOneWarningThatNamesTheFamilyButNotTheToken()
    {
        var signedIn = await SignIn();

        await Rotate(signedIn);

        _clock.Now = _clock.Now.AddSeconds(30);

        await Check(signedIn.Token);

        var warning = Assert.Single(_log.Entries);

        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(signedIn.SessionId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(signedIn.Token, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFamilyThatHasEnded_RaisesNoSecondAlarm()
    {
        var signedIn = await SignIn();
        var second = await Rotate(signedIn);

        await Rotate(second);

        _clock.Now = _clock.Now.AddSeconds(30);

        var endedAt = _clock.Now;

        await Check(second.Token);

        _clock.Now = _clock.Now.AddMinutes(10);

        await Check(second.Token);

        Assert.Equal(endedAt, (await FindSession(signedIn.SessionId)).ExpiresAt);
        Assert.Single(_log.Entries);
    }

    // the person's own row already ends every session, so there is nothing left to raise an alarm about
    [Fact]
    public async Task AReplayAfterTheStampChanged_EndsNothing()
    {
        var signedIn = await SignIn();
        var expiresAt = (await FindSession(signedIn.SessionId)).ExpiresAt;

        await Rotate(signedIn);
        await ChangeUser(signedIn.UserId, user => user.EndAllSessions());

        _clock.Now = _clock.Now.AddSeconds(30);

        Assert.Null(await Check(signedIn.Token));
        Assert.Equal(expiresAt, (await FindSession(signedIn.SessionId)).ExpiresAt);
        Assert.Empty(_log.Entries);
    }

    // the rotation read its row before the family ended and saved after, so only the first row can stop its token
    [Fact]
    public async Task ARotationSavedAfterItsFamilyEnded_OpensNoSession()
    {
        var signedIn = await SignIn();

        await using var late = postgres.CreateScope();
        var lateContext = Context(late);
        var stale = await lateContext.SessionTokens.SingleAsync(session => session.Id == signedIn.SessionId, Token);

        await EndFamily(signedIn.SessionId);

        var (token, hash) = SessionTokenFormat.Create();

        lateContext.SessionTokens.Add(stale.Rotate(hash, _clock.Now));

        await lateContext.SaveChangesAsync(Token);

        Assert.Null(await Check(token));
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

    // a scope per check, so every check reads the database and not a copy an earlier one tracked
    private async Task<SessionToken?> Check(string token)
    {
        await using var scope = postgres.CreateScope();

        return await new CheckSession(Context(scope), _clock, _log).HandleAsync(token, Token);
    }

    private async Task ChangeUser(Guid userId, Action<User> change)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Token);

        change(user);

        await context.SaveChangesAsync(Token);
    }

    private async Task EndFamily(Guid familyId)
    {
        await using var scope = postgres.CreateScope();

        await SessionFamily.EndAsync(Context(scope), familyId, _clock.Now, Token);
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

    private async Task<SessionToken> FindSession(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private sealed record SignedIn(string Token, Guid SessionId, Guid UserId);
}
