using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Sessions;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignInUserTests(PostgresFixture postgres) : IDisposable
{
    private const string Password = "correct horse battery staple";

    private static readonly AuditOrigin TestAuditOrigin =
        new(IPAddress.Parse("203.0.113.7"), "51e82f01e6b0966e2cc88ad6e173b22f");

    private readonly PasswordHasher _hasher = new(19456, 2, 1);
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheRightPassword_ReturnsTheSessionAndRecordsTheSignIn()
    {
        var email = await Seed();

        var result = await SignIn(email, Password);

        Assert.Equal(email, result.Session.Email);
        Assert.Equal("Ada Lovelace", result.Session.FullName);
        Assert.Equal(_clock.Now, (await Find(email)).LastLoginAt);
    }

    [Fact]
    public async Task TheRightPassword_StoresASessionForTheReturnedToken()
    {
        var email = await Seed();

        var result = await SignIn(email, Password);

        var user = await Find(email);
        var session = await FindSession(result.Token);

        Assert.Equal(user.Id, result.Session.UserId);
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(user.SecurityStamp, session.SecurityStamp);
        Assert.Equal(_clock.Now, session.CreatedAt);
    }

    [Fact]
    public async Task TheRightPassword_SavesOneRowForTheSignIn()
    {
        await postgres.ResetAsync(Token);

        var email = await Seed();

        await SignIn(email, Password);

        var user = await Find(email);
        var row = await TheOnlyRow();

        AssertSignInRow(row, AuditOutcome.Succeeded, errorCode: null, actor: user, target: user);
    }

    [Fact]
    public async Task AWrongPassword_SavesOneFailedRow()
    {
        await postgres.ResetAsync(Token);

        var email = await Seed();

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, "wrong horse battery staple"));

        var user = await Find(email);
        var row = await TheOnlyRow();

        AssertSignInRow(row, AuditOutcome.Failed, ErrorCodes.InvalidCredentials, actor: null, target: user);
    }

    [Fact]
    public async Task AnUnknownAddress_SavesOneFailedRow()
    {
        await postgres.ResetAsync(Token);

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(UniqueEmail(), Password));

        var row = await TheOnlyRow();

        AssertSignInRow(row, AuditOutcome.Failed, ErrorCodes.InvalidCredentials, actor: null, target: null);
    }

    [Fact]
    public async Task AnInactiveAccount_WithTheRightPassword_SavesOneFailedRow()
    {
        await postgres.ResetAsync(Token);

        var email = await Seed(active: false);

        await AssertRefused(ErrorCodes.AccountInactive, () => SignIn(email, Password));

        var user = await Find(email);
        var row = await TheOnlyRow();

        AssertSignInRow(row, AuditOutcome.Failed, ErrorCodes.AccountInactive, actor: user, target: user);
    }

    // the lockout is read before the failure is counted, so the fifth wrong password is still saved as a wrong password
    [Fact]
    public async Task WhileLocked_TheAttemptIsSavedAsLocked()
    {
        await postgres.ResetAsync(Token);

        var email = await Seed();

        await FailTimes(email, 5);
        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, Password));

        var user = await Find(email);
        var rows = await Rows();
        var locked = Assert.Single(rows, row => row.ErrorCode == ErrorCodes.AccountLocked);

        Assert.Equal(6, rows.Count);
        Assert.Equal(5, rows.Count(row => row.ErrorCode == ErrorCodes.InvalidCredentials));
        AssertSignInRow(locked, AuditOutcome.Failed, ErrorCodes.AccountLocked, actor: null, target: user);
    }

    // an unknown address has no counter to save, so only its row makes the refusal cost the database what a wrong password costs
    [Fact]
    public async Task ARefusal_SendsOneSaveWhetherTheAddressIsKnownOrNot()
    {
        var email = await Seed();

        AssertOneReadThenOneSave(await StatementsOfARefusal(email));
        AssertOneReadThenOneSave(await StatementsOfARefusal(UniqueEmail()));
    }

    // this path saves the failure, so a session added before the password check would be saved with it
    [Fact]
    public async Task AWrongPassword_StoresNoSession()
    {
        var email = await Seed();

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, "wrong horse battery staple"));

        Assert.Equal(0, await CountSessions(email));
    }

    [Fact]
    public async Task ASecondSignIn_LeavesTheFirstSessionInPlace()
    {
        var email = await Seed();

        await SignIn(email, Password);
        await SignIn(email, Password);

        Assert.Equal(2, await CountSessions(email));
    }

    [Fact]
    public async Task TheRightPassword_ClearsEarlierFailures()
    {
        var email = await Seed();

        await FailTimes(email, 3);
        await SignIn(email, Password);

        Assert.Equal(0, (await Find(email)).FailedLoginAttempts);
    }

    [Fact]
    public async Task AWrongPassword_IsRefusedAndCounted()
    {
        var email = await Seed();

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, "wrong horse battery staple"));

        Assert.Equal(1, (await Find(email)).FailedLoginAttempts);
    }

    [Fact]
    public async Task AMissingPassword_IsRefusedAndCounted()
    {
        var email = await Seed();

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, null));

        Assert.Equal(1, (await Find(email)).FailedLoginAttempts);
    }

    [Fact]
    public async Task AnUnknownAddress_GetsTheSameRefusal()
    {
        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(UniqueEmail(), Password));
    }

    // with the only slot taken the hasher gives up, so a busy answer proves the address did reach it
    [Fact]
    public async Task AnUnknownAddress_StillRunsTheHasher()
    {
        using var slow = new PasswordHasher(65536, 6, 1) { SlotWait = TimeSpan.FromMilliseconds(20) };

        var holding = slow.HashAsync(Password, Token);

        await AssertRefused(ErrorCodes.ServerBusy, () => SignIn(UniqueEmail(), Password, slow));

        await holding;
    }

    [Fact]
    public async Task TheFifthWrongPassword_LocksTheAccount()
    {
        var email = await Seed();

        await FailTimes(email, 5);

        Assert.Equal(_clock.Now.AddSeconds(30), (await Find(email)).LockoutUntil);
    }

    // the same work as a wrong password, so the time of the save cannot confirm a right guess
    [Fact]
    public async Task WhileLocked_TheRightPasswordIsRefusedAndPushesTheLockoutOut()
    {
        var email = await Seed();

        await FailTimes(email, 5);
        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, Password));

        var user = await Find(email);

        Assert.Equal(6, user.FailedLoginAttempts);
        Assert.Equal(_clock.Now.AddSeconds(60), user.LockoutUntil);
    }

    [Fact]
    public async Task OnceTheLockoutHasPassed_TheRightPasswordSignsIn()
    {
        var email = await Seed();

        await FailTimes(email, 5);

        _clock.Now = _clock.Now.AddSeconds(31);

        await SignIn(email, Password);

        var user = await Find(email);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutUntil);
    }

    [Fact]
    public async Task AnInactiveAccount_WithTheRightPassword_IsToldWhy()
    {
        var email = await Seed(active: false);

        await AssertRefused(ErrorCodes.AccountInactive, () => SignIn(email, Password));
    }

    // without the right password the reason stays hidden, or the answer would show the account exists
    [Fact]
    public async Task AnInactiveAccount_WithAWrongPassword_GetsTheOrdinaryRefusal()
    {
        var email = await Seed(active: false);

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, "wrong horse battery staple"));
    }

    [Fact]
    public async Task ADeletedAccount_GetsTheOrdinaryRefusal()
    {
        var email = await Seed();

        await using (var scope = postgres.CreateScope())
        {
            var context = Context(scope);
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email, Token);

            user.SoftDelete(_clock.Now, null);

            await context.SaveChangesAsync(Token);
        }

        await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, Password));
    }

    [Fact]
    public async Task TheAddress_IsMatchedWhateverItsCaseAndSurroundingSpaces()
    {
        var email = await Seed();

        var result = await SignIn($"  {email.ToUpperInvariant()} ", Password);

        Assert.Equal(email, result.Session.Email);
    }

    [Fact]
    public async Task AHashMadeAtALowerCost_IsUpgradedWithoutRotatingTheStamp()
    {
        using var cheaper = new PasswordHasher(8192, 1, 1);

        var email = await Seed(hasher: cheaper);
        var before = await Find(email);

        await SignIn(email, Password);

        var after = await Find(email);

        Assert.NotEqual(before.PasswordHash, after.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, await _hasher.VerifyAsync(Password, after.PasswordHash, Token));
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
    }

    public void Dispose() => _hasher.Dispose();

    private async Task<string> Seed(bool active = true, PasswordHasher? hasher = null)
    {
        var email = UniqueEmail();
        var user = User.CreateOwner(email, "Ada", "Lovelace", await (hasher ?? _hasher).HashAsync(Password, Token), _clock.Now);

        if (!active)
        {
            user.Deactivate();
        }

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);

        await context.SaveChangesAsync(Token);

        return email;
    }

    private async Task<SignInResult> SignIn(string email, string? password, PasswordHasher? hasher = null)
    {
        await using var scope = postgres.CreateScope();

        return await new SignInUser(Context(scope), hasher ?? _hasher, _clock)
            .HandleAsync(
                new SignInRequest { Email = email, Password = password },
                TestAuditOrigin,
                Token);
    }

    private async Task FailTimes(string email, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            await AssertRefused(ErrorCodes.InvalidCredentials, () => SignIn(email, "wrong horse battery staple"));
        }
    }

    private async Task<List<string>> StatementsOfARefusal(string email)
    {
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        await AssertRefused(
            ErrorCodes.InvalidCredentials,
            () => new SignInUser(context, _hasher, _clock).HandleAsync(
                new SignInRequest { Email = email, Password = "wrong horse battery staple" }, TestAuditOrigin, Token));

        return log.Statements;
    }

    private static void AssertOneReadThenOneSave(List<string> statements) =>
        Assert.Collection(
            statements,
            statement => Assert.StartsWith("SELECT", statement, StringComparison.Ordinal),
            statement => Assert.Contains("INSERT INTO audit_entries", statement, StringComparison.Ordinal));

    private async Task<User> Find(string email)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).Users.IgnoreQueryFilters().SingleAsync(user => user.Email == email, Token);
    }

    // found by the hash of the token, the way every signed-in request finds it
    private async Task<SessionToken> FindSession(string token)
    {
        var hash = SessionTokenFormat.HashOf(token);

        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.TokenHash == hash, Token);
    }

    // after a reset the table holds only this test's rows, so exactly one row means exactly one was saved
    private async Task<AuditEntry> TheOnlyRow() => Assert.Single(await Rows());

    private Task<List<AuditEntry>> Rows() => postgres.AuditRowsAsync(Token);

    // a null actor or target means its columns must be empty, not that they go unchecked
    private void AssertSignInRow(AuditEntry row, AuditOutcome outcome, string? errorCode, User? actor, User? target)
    {
        Assert.Equal(AuditActions.SignIn, row.Action);
        Assert.Equal(outcome, row.Outcome);
        Assert.Equal(errorCode, row.ErrorCode);
        Assert.Equal(actor?.Id, row.ActorId);
        Assert.Equal(actor?.FullName, row.ActorName);
        Assert.Equal(actor?.Email, row.ActorEmail);
        Assert.Equal(target is null ? null : AuditTargets.User, row.TargetType);
        Assert.Equal(target?.Id, row.TargetId);
        Assert.Equal(target?.FullName, row.TargetName);
        Assert.Equal(TestAuditOrigin.IPAddress, row.IPAddress);
        Assert.Equal(TestAuditOrigin.CorrelationId, row.CorrelationId);
        Assert.Equal(_clock.Now, row.OccurredAt);
    }

    private async Task<int> CountSessions(string email)
    {
        var user = await Find(email);

        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.CountAsync(session => session.UserId == user.Id, Token);
    }

    private static async Task AssertRefused(string code, Func<Task> signIn)
    {
        var refused = await Assert.ThrowsAnyAsync<MosaicException>(signIn);

        Assert.Equal(code, refused.Code);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";
}
