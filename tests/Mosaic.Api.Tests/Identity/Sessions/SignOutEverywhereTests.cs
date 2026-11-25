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
public sealed class SignOutEverywhereTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly AuditOrigin TestAuditOrigin =
        new(IPAddress.Parse("203.0.113.7"), "51e82f01e6b0966e2cc88ad6e173b22f");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SigningOutEverywhere_SavesOneRowForThePerson()
    {
        await postgres.ResetAsync(Token);

        var userId = await Seed();

        await SignOutEverywhere(userId);

        var user = await Find(userId);
        var row = Assert.Single(await postgres.AuditRowsAsync(Token));

        Assert.Equal(AuditActions.SignOutEverywhere, row.Action);
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

    private async Task<Guid> Seed()
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused by sign-out", Now);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(user);

        await context.SaveChangesAsync(Token);

        return user.Id;
    }

    private async Task SignOutEverywhere(Guid userId)
    {
        await using var scope = postgres.CreateScope();

        await new SignOutEverywhere(Context(scope), new Clock(Now)).HandleAsync(userId, TestAuditOrigin, Token);
    }

    private async Task<User> Find(Guid userId)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).Users.SingleAsync(user => user.Id == userId, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
}
