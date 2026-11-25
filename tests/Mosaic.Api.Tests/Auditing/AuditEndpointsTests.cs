using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Auditing;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class AuditEndpointsTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Entries = ApiPaths.Audit + AuditRoutes.Entries;
    private const string Filters = ApiPaths.Audit + AuditRoutes.Filters;
    private const string People = ApiPaths.Audit + AuditRoutes.People;

    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the answer reaches the machine that asked, so leaving the address off the screen would not hide it
    [Fact]
    public async Task OnlyTheOwnersAnswer_NamesTheAddress()
    {
        var seeded = await Seed();
        var client = Client();

        var owners = await Get(client, Entries, seeded.OwnerToken);
        var members = await Get(client, Entries, seeded.MemberToken);

        Assert.Equal(HttpStatusCode.OK, owners.StatusCode);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        Assert.Contains("\"ipAddress\":\"203.0.113.7\"", await owners.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.DoesNotContain("ipAddress", await members.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    // each other row fails exactly one of the four filters, so a filter that does not arrive lets its row through
    [Fact]
    public async Task TheFourFiltersInTheAddress_ReachTheList()
    {
        var seeded = await Seed();
        var tenInBerlin = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.FromHours(1));
        var query = $"?{AuditFilter.SucceededParameter}=true"
            + $"&{AuditFilter.ActionParameter}={AuditActions.SignIn}"
            + $"&{AuditFilter.PersonParameter}={seeded.MemberId}"
            + $"&{AuditFilter.FromParameter}={Uri.EscapeDataString(tenInBerlin.ToString("O", CultureInfo.InvariantCulture))}";

        var response = await Get(Client(), Entries + query, seeded.OwnerToken);
        var page = (await response.Content.ReadFromJsonAsync<ApiResponse<PageResult<AuditEntryResult>>>(MosaicJson.Options, Token))!.Data!;

        Assert.Equal(seeded.MatchId, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("?" + Paging.PageParameter + "=0", "page", ErrorCodes.TooSmall, "1")]
    [InlineData("?" + Paging.PageParameter + "=21474837", "page", ErrorCodes.TooLarge, "21474836")]
    [InlineData("?" + Paging.PageSizeParameter + "=0", "pageSize", ErrorCodes.TooSmall, "1")]
    [InlineData("?" + Paging.PageSizeParameter + "=101", "pageSize", ErrorCodes.TooLarge, "100")]
    public async Task APageOrASizeOutsideTheLimits_Is400WithTheFieldAndItsLimit(string query, string field, string code, string limit)
    {
        var seeded = await Seed();

        var response = await Get(Client(), Entries + query, seeded.OwnerToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);
        var error = Assert.Single(body!.Error!.FieldErrors![field]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error.Code);
        Assert.Equal(code, error.Code);
        Assert.Equal(limit, error.Params![ErrorParams.Limit]);
    }

    [Theory]
    [InlineData(AuditFilter.SucceededParameter)]
    [InlineData(AuditFilter.PersonParameter)]
    [InlineData(AuditFilter.FromParameter)]
    public async Task AValueThatCannotBeRead_Is400(string parameter)
    {
        var seeded = await Seed();

        var response = await Get(Client(), $"{Entries}?{parameter}=abc", seeded.OwnerToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Error!.Code);
    }

    [Theory]
    [InlineData(AuditFilter.SucceededParameter)]
    [InlineData(AuditFilter.PersonParameter)]
    [InlineData(AuditFilter.FromParameter)]
    [InlineData(AuditFilter.ActionParameter)]
    public async Task AnEmptyValue_Is400(string parameter)
    {
        var seeded = await Seed();

        var response = await Get(Client(), $"{Entries}?{parameter}=", seeded.OwnerToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Error!.Code);
    }

    // byte for byte what an installed desktop reads
    [Fact]
    public async Task TheChoices_AreTheActionsTheRowsHold()
    {
        var seeded = await Seed();

        var response = await Get(Client(), Filters, seeded.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"data":{"actions":["core.auth.sign_in","core.auth.sign_out"]},"success":true}""",
            await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task ThePeopleSearch_ReadsItsTextFromTheAddress()
    {
        var seeded = await Seed();

        var response = await Get(Client(), $"{People}?{AuditPeople.SearchParameter}=kent", seeded.OwnerToken);
        var people = (await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AuditPersonResult>>>(MosaicJson.Options, Token))!.Data!;

        Assert.Equal(seeded.MemberId, Assert.Single(people).Id);
    }

    // the people and their sessions are written straight to the database, so no sign-in adds a row to the log
    private async Task<Seeded> Seed()
    {
        await postgres.ResetAsync(Token);

        var now = TimeProvider.System.GetUtcNow();
        var owner = User.CreateOwner(UniqueEmail(), "Bruce", "Wayne", "unused here", now);
        var member = User.CreateOwner(UniqueEmail(), "Clark", "Kent", "unused here", now);
        var (ownerToken, ownerHash) = SessionTokenFormat.Create();
        var (memberToken, memberHash) = SessionTokenFormat.Create();
        var match = AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(member), TargetOf(member), Origin(), Monday.AddMinutes(30));

        member.SetOwner(false);

        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.Users.AddRange(owner, member);
        context.SessionTokens.AddRange(SessionToken.Issue(owner, ownerHash, now), SessionToken.Issue(member, memberHash, now));
        context.AuditEntries.AddRange(
            match,
            AuditEntry.Failed(AuditActions.SignIn, ErrorCodes.InvalidCredentials, null, TargetOf(member), Origin(), Monday.AddMinutes(40)),
            AuditEntry.Succeeded(AuditActions.SignOut, ActorOf(member), TargetOf(member), Origin(), Monday.AddMinutes(50)),
            AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(owner), TargetOf(owner), Origin(), Monday.AddMinutes(20)),
            AuditEntry.Succeeded(AuditActions.SignIn, ActorOf(member), TargetOf(member), Origin(), Monday.AddMinutes(-30)));

        await context.SaveChangesAsync(Token);

        return new Seeded(ownerToken, memberToken, member.Id, match.Id);
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, Token);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();

    private static AuditActor ActorOf(User user) => new(user.Id, user.FullName, user.Email);

    private static AuditTarget TargetOf(User user) => new(AuditTargets.User, user.Id, user.FullName);

    private static AuditOrigin Origin() => new(IPAddress.Parse("203.0.113.7"), Guid.CreateVersion7().ToString("N"));

    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";

    private sealed record Seeded(string OwnerToken, string MemberToken, Guid MemberId, Guid MatchId);
}
