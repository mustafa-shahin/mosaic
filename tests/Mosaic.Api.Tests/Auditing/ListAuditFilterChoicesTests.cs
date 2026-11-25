using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Auditing;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Auditing;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class ListAuditFilterChoicesTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheChoices_AreTheActionsOfTheRows_EachOnceAndInOrder()
    {
        await postgres.ResetAsync(Token);
        await Store(AuditActions.SignOutEverywhere, AuditActions.SignIn, AuditActions.SignOut, AuditActions.SignIn, AuditActions.Register);

        var choices = await Choices();

        Assert.Equal(
            [AuditActions.Register, AuditActions.SignIn, AuditActions.SignOut, AuditActions.SignOutEverywhere],
            choices.Actions);
    }

    [Fact]
    public async Task AnEmptyLog_OffersNoAction()
    {
        await postgres.ResetAsync(Token);

        var choices = await Choices();

        Assert.Empty(choices.Actions);
    }

    private async Task Store(params string[] actions)
    {
        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        foreach (var action in actions)
        {
            context.AuditEntries.Add(AuditEntry.Succeeded(action, null, null, new AuditOrigin(null, Guid.CreateVersion7().ToString("N")), Now));
        }

        await context.SaveChangesAsync(Token);
    }

    private async Task<AuditFilterChoices> Choices()
    {
        await using var scope = postgres.CreateScope();

        return await new ListAuditFilterChoices(scope.ServiceProvider.GetRequiredService<ManagementDbContext>()).HandleAsync(Token);
    }
}
