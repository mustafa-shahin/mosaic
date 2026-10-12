using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Identity;
using Mosaic.Application.Security;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SessionFamilyTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset SignedInAt = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // two requests that both found the family alive both try to end it, and only the first one counts
    [Fact]
    public async Task EndingAFamilyTwice_WritesOnlyTheFirstTime()
    {
        var first = await SignIn();
        var endedAt = SignedInAt.AddHours(1);

        Assert.True(await End(first.Id, endedAt));
        Assert.False(await End(first.Id, endedAt.AddMinutes(1)));
        Assert.Equal(endedAt, (await Find(first.Id)).ExpiresAt);
    }

    [Fact]
    public async Task ReportingTheSameReplayTwice_LogsOneLine()
    {
        var first = await SignIn();
        var log = new RecordingLogger<SessionFamilyTests>();

        await Report(first, log, SignedInAt.AddHours(1));
        await Report(first, log, SignedInAt.AddHours(1).AddSeconds(1));

        Assert.Single(log.Entries);
    }

    // the person and the session are written straight to the database, the way sign-in leaves them
    private async Task<SessionToken> SignIn()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused here", SignedInAt);
        var session = SessionToken.Issue(user, SessionTokenFormat.Create().Hash, SignedInAt);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);
        context.SessionTokens.Add(session);

        await context.SaveChangesAsync(Token);

        return session;
    }

    private async Task<bool> End(Guid familyId, DateTimeOffset now)
    {
        await using var scope = postgres.CreateScope();

        return await SessionFamily.EndAsync(Context(scope), familyId, now, Token);
    }

    private async Task Report(SessionToken replayed, RecordingLogger<SessionFamilyTests> log, DateTimeOffset now)
    {
        await using var scope = postgres.CreateScope();

        await SessionFamily.EndForReuseAsync(Context(scope), log, replayed, now, Token);
    }

    private async Task<SessionToken> Find(Guid sessionId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).SessionTokens.SingleAsync(session => session.Id == sessionId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
}
