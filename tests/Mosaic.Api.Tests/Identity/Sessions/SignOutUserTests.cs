using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Sessions;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignOutUserTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly AuditOrigin TestAuditOrigin =
        new(IPAddress.Parse("203.0.113.7"), "51e82f01e6b0966e2cc88ad6e173b22f");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SigningOut_DeletesThisSessionsRow()
    {
        var signedIn = await SignIn();

        await SignOut(signedIn.SessionId);

        Assert.False(await Exists(signedIn.SessionId));
    }

    [Fact]
    public async Task SigningOut_SavesOneRowForThePerson()
    {
        await postgres.ResetAsync(Token);

        var signedIn = await SignIn();

        await SignOut(signedIn.SessionId);

        var user = await FindUser(signedIn.UserId);
        var row = Assert.Single(await postgres.AuditRowsAsync(Token));

        Assert.Equal(AuditActions.SignOut, row.Action);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.ErrorCode);
        Assert.Equal(user.Id, row.ActorId);
        Assert.Equal(user.FullName, row.ActorName);
        Assert.Equal(user.Email, row.ActorEmail);
        Assert.Equal(AuditTargets.User, row.TargetType);
        Assert.Equal(user.Id, row.TargetId);
        Assert.Equal(user.FullName, row.TargetName);
        Assert.Equal(TestAuditOrigin.IPAddress, row.IPAddress);
        Assert.Equal(TestAuditOrigin.CorrelationId, row.CorrelationId);
        Assert.Equal(Now, row.OccurredAt);
    }

    [Fact]
    public async Task SigningOut_LeavesThePersonsOtherSessionAsItWas()
    {
        var signedIn = await SignIn();
        var other = await SignInAgain(signedIn);
        var before = await FindSession(other.SessionId);

        await SignOut(signedIn.SessionId);

        var after = await FindSession(other.SessionId);

        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Null(after.UpdatedAt);
    }

    // both requests passed the check before either one deleted the row, so the second must not fail
    [Fact]
    public async Task TwoSignOutsAtTheSameMoment_BothEndWithoutAnError()
    {
        var signedIn = await SignIn();

        await using var first = postgres.CreateScope();
        await using var second = postgres.CreateScope();

        Assert.NotNull(await Check(Context(first), signedIn.Token));
        Assert.NotNull(await Check(Context(second), signedIn.Token));

        await new SignOutUser(Context(first), new Clock(Now)).HandleAsync(signedIn.SessionId, TestAuditOrigin, Token);
        await new SignOutUser(Context(second), new Clock(Now)).HandleAsync(signedIn.SessionId, TestAuditOrigin, Token);

        Assert.False(await Exists(signedIn.SessionId));
    }

    [Fact]
    public async Task WhenTheRowFailsToSave_TheSessionStays()
    {
        var signedIn = await SignIn();

        // one character more than the column holds, so the database refuses the row
        var tooLong = new AuditOrigin(IPAddress.Parse("203.0.113.7"), new string('x', AuditEntry.CorrelationIdMaxLength + 1));

        await using var scope = postgres.CreateScope();
        var signOutUser = new SignOutUser(Context(scope), new Clock(Now));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => signOutUser.HandleAsync(signedIn.SessionId, tooLong, Token));

        Assert.True(await Exists(signedIn.SessionId));
    }

    // the person and the session are written straight to the database, the way sign-in leaves them
    private async Task<SignedIn> SignIn()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by sign-out", Now);
        var (token, hash) = SessionTokenFormat.Create();
        var session = SessionToken.Issue(user, hash, Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, session.Id, user.Id);
    }

    // a second sign-in of the same person, on another machine
    private async Task<SignedIn> SignInAgain(SignedIn signedIn)
    {
        var (token, hash) = SessionTokenFormat.Create();

        await using var scope = postgres.CreateScope();
        var context = Context(scope);
        var user = await context.Users.SingleAsync(candidate => candidate.Id == signedIn.UserId, Token);
        var session = SessionToken.Issue(user, hash, Now);

        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return new SignedIn(token, session.Id, user.Id);
    }

    private async Task SignOut(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        await new SignOutUser(Context(scope), new Clock(Now)).HandleAsync(sessionId, TestAuditOrigin, Token);
    }

    private static Task<SessionToken?> Check(ManagementDbContext context, string token) =>
        new CheckSession(context, new Clock(Now)).HandleAsync(token, Token);

    private async Task<SessionToken> FindSession(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private async Task<User> FindUser(Guid userId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).Users.SingleAsync(user => user.Id == userId, Token);
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
