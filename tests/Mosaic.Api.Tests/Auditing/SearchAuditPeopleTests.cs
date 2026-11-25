using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Auditing;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Auditing;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SearchAuditPeopleTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("mar", "Martha Kent, Martha Wayne")]
    [InlineData("martha w", "Martha Wayne")]
    [InlineData("Bruce Way", "Bruce Wayne")]
    [InlineData("WAYNE", "Bruce Wayne, Martha Wayne")]
    [InlineData("prince@", "Diana Prince")]
    [InlineData("  prince@  ", "Diana Prince")]
    public async Task TheText_IsFoundInTheFullNameOrTheEmail_InAnyCase(string text, string found)
    {
        await Seed();

        Assert.Equal(found, Names(await Search(text)));
    }

    // ILIKE reads both as wildcards, so typed as they are they would match everyone
    [Theory]
    [InlineData("_", "Barbara Gordon, Tim Drake")]
    [InlineData("%", "")]
    public async Task AnUnderscoreOrAPercentSign_MatchesOnlyItself(string text, string found)
    {
        await Seed();

        Assert.Equal(found, Names(await Search(text)));
    }

    [Fact]
    public async Task ADeactivatedAccount_IsFound_ADeletedOneIsNot()
    {
        await Seed();

        Assert.Equal("Barry Allen", Names(await Search("barry")));
        Assert.Empty(await Search("selina"));
    }

    [Fact]
    public async Task AtMostTenPeople_AreSuggested_InTheOrderOfTheirNames()
    {
        await Seed();

        var found = await Search("lantern");

        Assert.Equal(10, found.Count);
        Assert.Equal("Green Lantern 01", found[0].Name);
        Assert.Equal("Green Lantern 10", found[^1].Name);
    }

    [Fact]
    public async Task ASuggestion_CarriesTheIdTheFullNameAndTheEmail()
    {
        var bruce = await Seed();

        var person = Assert.Single(await Search("bruce"));

        Assert.Equal(bruce.Id, person.Id);
        Assert.Equal("Bruce Wayne", person.Name);
        Assert.Equal("bruce.wayne@example.com", person.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoText_SuggestsNobody_WithoutAskingTheDatabase(string? text)
    {
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        Assert.Empty(await new SearchAuditPeople(context).HandleAsync(text, Token));
        Assert.Empty(log.Statements);
    }

    private async Task<User> Seed()
    {
        await postgres.ResetAsync(Token);

        var bruce = Account("bruce.wayne@example.com", "Bruce", "Wayne");
        var barry = Account("barry.allen@example.com", "Barry", "Allen");
        var selina = Account("selina.kyle@example.com", "Selina", "Kyle");

        barry.Deactivate();
        selina.SoftDelete(Now, null);

        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.Users.AddRange(
            bruce,
            barry,
            selina,
            Account("martha.wayne@example.com", "Martha", "Wayne"),
            Account("martha.kent@example.com", "Martha", "Kent"),
            Account("diana.prince@example.com", "Diana", "Prince"),
            Account("tim_drake@example.com", "Tim", "Drake"),
            Account("barbara_gordon@example.com", "Barbara", "Gordon"));

        // stored last first, so the order of the answer comes from the sort
        for (var number = 12; number >= 1; number--)
        {
            context.Users.Add(Account($"lantern{number}@example.com", "Green", $"Lantern {number:00}"));
        }

        await context.SaveChangesAsync(Token);

        return bruce;
    }

    private async Task<IReadOnlyList<AuditPersonResult>> Search(string text)
    {
        await using var scope = postgres.CreateScope();

        return await new SearchAuditPeople(scope.ServiceProvider.GetRequiredService<ManagementDbContext>()).HandleAsync(text, Token);
    }

    private static User Account(string email, string firstName, string lastName) =>
        User.CreateOwner(email, firstName, lastName, "unused by the search", Now);

    private static string Names(IReadOnlyList<AuditPersonResult> people) =>
        string.Join(", ", people.Select(person => person.Name));
}
