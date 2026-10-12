using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Identity;
using Mosaic.Application.Security;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class RotateSessionTests(PostgresFixture postgres)
{
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    private readonly RecordingLogger<RotateSession> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rotating_StoresANewRowForTheReturnedTokenInTheSameFamily()
    {
        var sessionId = await SignIn();

        _clock.Now = _clock.Now.AddHours(1);

        var result = await Rotate(sessionId);

        var old = await FindById(sessionId);
        var next = await FindByToken(result.Token);

        Assert.Equal(_clock.Now, old.RevokedAt);
        Assert.Equal(sessionId, next.ReplacesId);
        Assert.Equal(old.FamilyId, next.FamilyId);
        Assert.Equal(old.ExpiresAt, next.ExpiresAt);
    }

    [Fact]
    public async Task ATokenAlreadyReplaced_IsRefused_AndItsFamilyEnds()
    {
        var sessionId = await SignIn();

        await Rotate(sessionId);

        await AssertReplayed(() => Rotate(sessionId));

        Assert.Equal(_clock.Now, (await FindById(sessionId)).ExpiresAt);
        Assert.Single(_log.Entries);
    }

    // the second request read the row before the first one saved, so only the index can stop it
    [Fact]
    public async Task TwoRotationsAtOnce_SaveOneNewRow_AndEndTheFamily()
    {
        var sessionId = await SignIn();

        await using var late = postgres.CreateScope();
        var lateContext = Context(late);

        await lateContext.SessionTokens.SingleAsync(session => session.Id == sessionId, Token);

        await Rotate(sessionId);

        await AssertReplayed(() => new RotateSession(lateContext, _clock, _log).HandleAsync(sessionId, Token));

        Assert.Equal(1, await RowsReplacing(sessionId));
        Assert.Equal(_clock.Now, (await FindById(sessionId)).ExpiresAt);
    }

    // the person and the session are written straight to the database, the way sign-in leaves them
    private async Task<Guid> SignIn()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by rotation", _clock.Now);
        var session = SessionToken.Issue(user, SessionTokenFormat.Create().Hash, _clock.Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return session.Id;
    }

    private async Task<RotateResult> Rotate(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await new RotateSession(Context(scope), _clock, _log).HandleAsync(sessionId, Token);
    }

    private async Task<SessionToken> FindById(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private async Task<SessionToken> FindByToken(string token)
    {
        var hash = SessionTokenFormat.HashOf(token);

        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.TokenHash == hash, Token);
    }

    private async Task<int> RowsReplacing(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.CountAsync(session => session.ReplacesId == sessionId, Token);
    }

    private static async Task AssertReplayed(Func<Task> rotate)
    {
        var refused = await Assert.ThrowsAsync<NotPermittedException>(rotate);

        Assert.Equal(ErrorCodes.SessionReplayed, refused.Code);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
}
