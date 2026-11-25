using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Auditing;
using Mosaic.Api.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Auditing;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class ListAuditEntriesTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static readonly PageRequest FirstPage = new() { Page = Paging.FirstPage, PageSize = Paging.DefaultPageSize };

    private static readonly AuditFilter NoFilter = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePage_IsNewestFirst()
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, NoFilter);

        Assert.Equal(
            [Monday.AddHours(2), Monday.AddHours(1), Monday, Monday.AddHours(-1), Monday.AddDays(-3)],
            page.Items.Select(item => item.OccurredAt));
    }

    // sorted by the time alone, rows of one moment could change places between two pages and show twice
    [Fact]
    public async Task RowsOfTheSameMoment_AreNewestIdFirst()
    {
        var owner = await SeedOwner();
        List<AuditEntry> entries = [SignInOf(owner, Monday), SignInOf(owner, Monday), SignInOf(owner, Monday)];

        entries.Sort((left, right) => string.CompareOrdinal(left.Id.ToString(), right.Id.ToString()));

        // stored oldest id first, so only the sort by id can turn the order around
        foreach (var entry in entries)
        {
            await Store(entry);
        }

        // read through the time index the rows come in id order anyway; told the table is tiny, Postgres sorts them itself
        await Analyze();

        var page = await List(owner.Id, FirstPage, NoFilter);

        Assert.Equal([entries[2].Id, entries[1].Id, entries[0].Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task APage_SkipsTheRowsOfTheEarlierPages()
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, new PageRequest { Page = 2, PageSize = 2 }, NoFilter);

        Assert.Equal([Monday, Monday.AddHours(-1)], page.Items.Select(item => item.OccurredAt));
        Assert.Equal(5, page.TotalItems);
        Assert.Equal(2, page.ItemsPerPage);
        Assert.Equal(2, page.CurrentPage);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 1)]
    public async Task TheOutcomeFilter_KeepsTheRowsOfThatOutcome(bool succeeded, int count)
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, new AuditFilter { Succeeded = succeeded });

        Assert.Equal(count, page.TotalItems);
        Assert.Equal(count, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(succeeded, item.Succeeded));
    }

    [Theory]
    [InlineData(AuditActions.SignIn, 3)]
    [InlineData(AuditActions.SignOut, 1)]
    [InlineData(AuditActions.SignOutEverywhere, 0)]
    public async Task TheActionFilter_KeepsTheRowsOfThatAction(string action, int count)
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, new AuditFilter { Action = action });

        Assert.Equal(count, page.TotalItems);
        Assert.Equal(count, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(action, item.Action));
    }

    // a wrong password names no actor, only the account it was tried on
    [Fact]
    public async Task ThePersonFilter_KeepsTheRowsThePersonDidAndTheRowsDoneToThem()
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, new AuditFilter { Person = people.Owner.Id });

        Assert.Equal([Monday, Monday.AddHours(-1), Monday.AddDays(-3)], page.Items.Select(item => item.OccurredAt));
        Assert.Equal(3, page.TotalItems);
    }

    // Npgsql writes a time into a timestamptz only with the offset 0
    [Fact]
    public async Task TheTimeFilter_KeepsTheRowsFromThatMomentOn_WhateverItsOffset()
    {
        var people = await Seed();
        var tenInBerlin = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.FromHours(1));

        var page = await List(people.Owner.Id, FirstPage, new AuditFilter { From = tenInBerlin });

        Assert.Equal([Monday.AddHours(2), Monday.AddHours(1), Monday], page.Items.Select(item => item.OccurredAt));
        Assert.Equal(3, page.TotalItems);
    }

    [Fact]
    public async Task TheFilters_Combine()
    {
        var people = await Seed();
        var filter = new AuditFilter
        {
            Succeeded = false,
            Action = AuditActions.SignIn,
            Person = people.Owner.Id,
            From = Monday.AddDays(-1),
        };

        var page = await List(people.Owner.Id, FirstPage, filter);

        Assert.Equal(Monday.AddHours(-1), Assert.Single(page.Items).OccurredAt);
    }

    [Fact]
    public async Task ARow_CarriesTheCopiesItsEntryTook()
    {
        var people = await Seed();
        var stored = await postgres.AuditRowsAsync(Token);

        var page = await List(people.Owner.Id, FirstPage, NoFilter);

        var signIn = page.Items.Single(item => item.OccurredAt == Monday);
        var failed = page.Items.Single(item => !item.Succeeded);

        Assert.Equal(stored.Single(row => row.OccurredAt == Monday).Id, signIn.Id);
        Assert.Equal(AuditActions.SignIn, signIn.Action);
        Assert.True(signIn.Succeeded);
        Assert.Null(signIn.ErrorCode);
        Assert.Equal("Bruce Wayne", signIn.ActorName);
        Assert.Equal(people.Owner.Email, signIn.ActorEmail);
        Assert.Equal(AuditTargets.User, signIn.TargetType);
        Assert.Equal("Bruce Wayne", signIn.TargetName);
        Assert.Equal(stored.Single(row => row.OccurredAt == Monday).CorrelationId, signIn.CorrelationId);

        Assert.Equal(ErrorCodes.InvalidCredentials, failed.ErrorCode);
        Assert.Null(failed.ActorName);
        Assert.Null(failed.ActorEmail);
        Assert.Equal("Bruce Wayne", failed.TargetName);
        Assert.Equal(stored.Single(row => row.Outcome == AuditOutcome.Failed).CorrelationId, failed.CorrelationId);
    }

    [Fact]
    public async Task ALogWithRows_IsNotEmpty_AndTheCountAloneSaysSo()
    {
        var people = await Seed();
        var log = new StatementLog();

        var page = await List(people.Owner.Id, FirstPage, NoFilter, log);

        Assert.False(page.IsEmptyWithoutFilters);
        Assert.DoesNotContain(log.Statements, statement => statement.Contains("EXISTS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFilterThatMatchesNothing_LeavesALogWithRowsNotEmpty()
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, new AuditFilter { Action = AuditActions.SignOutEverywhere });

        Assert.Empty(page.Items);
        Assert.False(page.IsEmptyWithoutFilters);
    }

    [Fact]
    public async Task AnEmptyLog_IsEmpty_AndTheCountAloneSaysSo()
    {
        var owner = await SeedOwner();
        var log = new StatementLog();

        var page = await List(owner.Id, FirstPage, NoFilter, log);

        Assert.True(page.IsEmptyWithoutFilters);
        Assert.DoesNotContain(log.Statements, statement => statement.Contains("EXISTS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnEmptyLog_IsEmptyWithAFilterToo()
    {
        var owner = await SeedOwner();

        var page = await List(owner.Id, FirstPage, new AuditFilter { Succeeded = false });

        Assert.True(page.IsEmptyWithoutFilters);
    }

    // host() writes the bare address; a cast to text would add the prefix length, ::1/128
    [Fact]
    public async Task TheOwner_ReceivesTheAddressOfEachRow()
    {
        var people = await Seed();

        var page = await List(people.Owner.Id, FirstPage, NoFilter);

        Assert.Equal(["2001:db8::7", "2001:db8::7", "::1", "198.51.100.44", null], page.Items.Select(item => item.IPAddress));
    }

    // checked in the SQL too: for anyone but the owner the address must not leave the database
    [Fact]
    public async Task SomeoneWhoIsNotTheOwner_ReceivesNoAddress()
    {
        var people = await Seed();
        var log = new StatementLog();

        var page = await List(people.Member.Id, FirstPage, NoFilter, log);

        Assert.Equal(5, page.Items.Count);
        Assert.All(page.Items, item => Assert.Null(item.IPAddress));
        Assert.DoesNotContain(log.Statements, statement => statement.Contains("ip_address", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APageBeforeTheFirst_IsRefusedBeforeTheDatabaseIsAsked()
    {
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        var refused = await Assert.ThrowsAsync<ValidationException>(() =>
            new ListAuditEntries(context, new PageRequestValidator(), new AuditFilterValidator())
                .HandleAsync(Guid.CreateVersion7(), FirstPage with { Page = 0 }, NoFilter, Token));

        Assert.Equal(ErrorCodes.TooSmall, Assert.Single(refused.FieldErrors["page"]).Code);
        Assert.Empty(log.Statements);
    }

    [Fact]
    public async Task AnEmptyAction_IsRefusedBeforeTheDatabaseIsAsked()
    {
        var log = new StatementLog();

        await using var context = postgres.CreateLoggedContext(log);

        var refused = await Assert.ThrowsAsync<ValidationException>(() =>
            new ListAuditEntries(context, new PageRequestValidator(), new AuditFilterValidator())
                .HandleAsync(Guid.CreateVersion7(), FirstPage, new AuditFilter { Action = string.Empty }, Token));

        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(refused.FieldErrors["action"]).Code);
        Assert.Empty(log.Statements);
    }

    private async Task<PageResult<AuditEntryResult>> List(
        Guid callerId, PageRequest request, AuditFilter filter, StatementLog? log = null)
    {
        await using var context = postgres.CreateLoggedContext(log ?? new StatementLog());

        return await new ListAuditEntries(context, new PageRequestValidator(), new AuditFilterValidator())
            .HandleAsync(callerId, request, filter, Token);
    }

    private async Task<People> Seed()
    {
        var owner = await SeedOwner();
        var member = User.CreateOwner(UniqueEmail(), "Clark", "Kent", "unused by the audit list", Monday);

        member.SetOwner(false);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(member);
        context.AuditEntries.AddRange(
            AuditEntry.Succeeded(AuditActions.Register, ActorOf(owner), TargetOf(owner), Origin(null), Monday.AddDays(-3)),
            AuditEntry.Failed(AuditActions.SignIn, ErrorCodes.InvalidCredentials, null, TargetOf(owner), Origin("198.51.100.44"), Monday.AddHours(-1)),
            AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(owner), TargetOf(owner), Origin("::1"), Monday),
            AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(member), TargetOf(member), Origin("2001:db8::7"), Monday.AddHours(1)),
            AuditEntry.Succeeded(AuditActions.SignOut, ActorOf(member), TargetOf(member), Origin("2001:db8::7"), Monday.AddHours(2)));

        await context.SaveChangesAsync(Token);

        return new People(owner, member);
    }

    private async Task<User> SeedOwner()
    {
        await postgres.ResetAsync(Token);

        var owner = User.CreateOwner(UniqueEmail(), "Bruce", "Wayne", "unused by the audit list", Monday);

        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.Users.Add(owner);

        await context.SaveChangesAsync(Token);

        return owner;
    }

    private async Task Store(AuditEntry entry)
    {
        await using var scope = postgres.CreateScope();
        var context = Context(scope);

        context.AuditEntries.Add(entry);

        await context.SaveChangesAsync(Token);
    }

    private async Task Analyze()
    {
        await using var scope = postgres.CreateScope();

        await Context(scope).Database.ExecuteSqlRawAsync("ANALYZE audit_entries", Token);
    }

    private static AuditEntry SignInOf(User user, DateTimeOffset at) =>
        AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(user), TargetOf(user), Origin(null), at);

    private static AuditActor ActorOf(User user) => new(user.Id, user.FullName, user.Email);

    private static AuditTarget TargetOf(User user) => new(AuditTargets.User, user.Id, user.FullName);

    // every row gets its own trace id, so a row mixed up with another fails on it
    private static AuditOrigin Origin(string? address) =>
        new(address is null ? null : IPAddress.Parse(address), Guid.CreateVersion7().ToString("N"));

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";

    private sealed record People(User Owner, User Member);
}
