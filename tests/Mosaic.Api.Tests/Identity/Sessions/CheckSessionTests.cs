using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Sessions;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class CheckSessionTests(PostgresFixture postgres)
{
    private static readonly AuditOrigin TestAuditOrigin =
        new(IPAddress.Parse("203.0.113.7"), "51e82f01e6b0966e2cc88ad6e173b22f");

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
    public async Task TwelveHoursAfterTheSignIn_TheTokenIsRefused()
    {
        var signedIn = await SignIn();

        _clock.Now = _clock.Now.AddHours(12);

        Assert.Null(await Check(signedIn.Token));
    }

    // a check only reads, so using the session just before it ends never moves its end
    [Fact]
    public async Task ASessionUsedLate_StillEndsTwelveHoursAfterTheSignIn()
    {
        var signedIn = await SignIn();
        var signedInAt = _clock.Now;

        _clock.Now = signedInAt.AddHours(12).AddTicks(-1);

        Assert.NotNull(await Check(signedIn.Token));

        _clock.Now = signedInAt.AddHours(12);

        Assert.Null(await Check(signedIn.Token));
    }

    // the check leaves the person tracked, so reading them again in the same request costs no query
    [Fact]
    public async Task AfterACheck_ReadingTheSession_SendsNoSecondQuery()
    {
        var signedIn = await SignIn();
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        var session = await new CheckSession(context, _clock).HandleAsync(signedIn.Token, Token);

        await new GetSession(context).HandleAsync(session!.UserId, Token);

        Assert.StartsWith("SELECT", Assert.Single(log.Statements), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterACheck_SigningOutEverywhere_SendsNoSecondQuery()
    {
        var signedIn = await SignIn();
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        var session = await new CheckSession(context, _clock).HandleAsync(signedIn.Token, Token);

        await new SignOutEverywhere(context, _clock).HandleAsync(session!.UserId, TestAuditOrigin, Token);

        // one save is one command, and EF puts the row's INSERT before the UPDATE
        Assert.Collection(
            log.Statements,
            statement => Assert.StartsWith("SELECT", statement, StringComparison.Ordinal),
            statement => Assert.Contains("UPDATE users", statement, StringComparison.Ordinal));
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

    // every signed-in request runs the check, so each one here may send one SELECT and nothing else, refusals included
    private async Task<SessionToken?> Check(string token)
    {
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        var session = await new CheckSession(context, _clock).HandleAsync(token, Token);

        Assert.StartsWith("SELECT", Assert.Single(log.Statements), StringComparison.Ordinal);

        return session;
    }

    private async Task ChangeUser(Guid userId, Action<User> change)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Token);

        change(user);

        await context.SaveChangesAsync(Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private sealed record SignedIn(string Token, Guid SessionId, Guid UserId);
}
