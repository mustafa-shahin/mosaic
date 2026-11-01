using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Sessions;

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

        await using var context = Logged(log);

        var session = await new CheckSession(context, _clock).HandleAsync(signedIn.Token, Token);

        await new GetSession(context).HandleAsync(session!.UserId, Token);

        Assert.StartsWith("SELECT", Assert.Single(log.Statements), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterACheck_SigningOutEverywhere_SendsOnlyTheUpdate()
    {
        var signedIn = await SignIn();
        var log = new StatementLog();

        await using var context = Logged(log);

        var session = await new CheckSession(context, _clock).HandleAsync(signedIn.Token, Token);

        await new SignOutEverywhere(context).HandleAsync(session!.UserId, Token);

        Assert.Collection(
            log.Statements,
            statement => Assert.StartsWith("SELECT", statement, StringComparison.Ordinal),
            statement => Assert.StartsWith("UPDATE users", statement, StringComparison.Ordinal));
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

        await using var context = Logged(log);

        var session = await new CheckSession(context, _clock).HandleAsync(token, Token);

        Assert.StartsWith("SELECT", Assert.Single(log.Statements), StringComparison.Ordinal);

        return session;
    }

    // a context of its own for every use, so nothing an earlier check tracked can answer for the database
    private ManagementDbContext Logged(StatementLog log)
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>();

        DatabaseOptions.Configure(options, postgres.ConnectionString);
        options.AddInterceptors(log);

        return new ManagementDbContext(options.Options);
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

    // both the async and the plain methods, so a write cannot slip past by being synchronous
    private sealed class StatementLog : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Statements.Add(command.CommandText);

            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Statements.Add(command.CommandText);

            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Statements.Add(command.CommandText);

            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
