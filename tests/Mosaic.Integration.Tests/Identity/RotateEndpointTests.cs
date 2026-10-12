using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Security;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class RotateEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = "/v1/auth/rotate";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rotating_Is200WithANewTokenThatOpensTheSameSession()
    {
        var client = Client();
        var old = await SignedIn();

        var response = await Send(client, HttpMethod.Post, Path, old);
        var next = await NewToken(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(old, next);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/v1/auth/session", next)).StatusCode);
    }

    // a request that left with the old token just before the rotation still finishes
    [Fact]
    public async Task TheOldToken_StillOpensTheSessionInsideItsGrace()
    {
        var client = Client();
        var old = await SignedIn();

        await Send(client, HttpMethod.Post, Path, old);

        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/v1/auth/session", old)).StatusCode);
    }

    [Fact]
    public async Task RotatingTheOldTokenAgain_Is403_AndEndsTheNewTokenToo()
    {
        var client = Client();
        var old = await SignedIn();

        var next = await NewToken(await Send(client, HttpMethod.Post, Path, old));

        var again = await Send(client, HttpMethod.Post, Path, old);
        var body = await again.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
        Assert.Equal(ErrorCodes.SessionReplayed, body!.Error!.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/v1/auth/session", next)).StatusCode);
    }

    // the real desktop does not know its token was replaced, so whatever it sends next shows the copy
    [Fact]
    public async Task WhenAThiefRotatesFirst_TheRealDesktopsNextRequestEndsTheThiefsToken()
    {
        var clock = new Clock(TimeProvider.System.GetUtcNow());
        var client = Client(clock);
        var old = await SignedIn(clock.Now);

        var thiefs = await NewToken(await Send(client, HttpMethod.Post, Path, old));

        clock.Now = clock.Now.AddSeconds(30);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/v1/auth/session", old)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/v1/auth/session", thiefs)).StatusCode);
    }

    // a dead token never reaches the rotation code, so it gets the answer every endpoint gives it
    [Fact]
    public async Task RotatingWithATokenThatOpensNoSession_Is401()
    {
        var refused = await Send(Client(), HttpMethod.Post, Path, SessionTokenFormat.Create().Token);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal("Bearer realm=\"mosaic\", error=\"invalid_token\"", Assert.Single(refused.Headers.GetValues("WWW-Authenticate")));
    }

    // the test host sends every request from no address, so only a bucket keyed on the person keeps the two apart
    [Fact]
    public async Task EachPerson_HasTenRotationsOfTheirOwn()
    {
        var client = Client();
        var first = await SignedIn();
        var second = await SignedIn();

        for (var rotation = 0; rotation < 10; rotation++)
        {
            var response = await Send(client, HttpMethod.Post, Path, first);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            first = await NewToken(response);
        }

        var refused = await Send(client, HttpMethod.Post, Path, first);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("360", Assert.Single(refused.Headers.GetValues("Retry-After")));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, Path, second)).StatusCode);
    }

    // written straight to the database, so no test here spends the sign-in bucket
    private Task<string> SignedIn() => SignedIn(TimeProvider.System.GetUtcNow());

    private async Task<string> SignedIn(DateTimeOffset now)
    {
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused here", now);
        var (token, hash) = SessionTokenFormat.Create();

        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.Users.Add(user);
        context.SessionTokens.Add(SessionToken.Issue(user, hash, now));

        await context.SaveChangesAsync(Token);

        return token;
    }

    private static async Task<string> NewToken(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RotateResult>>(MosaicJson.Options, Token);

        return body!.Data!.Token;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client.SendAsync(request, Token);
    }

    // a host per test, so each starts with full buckets
    private HttpClient Client(TimeProvider? clock = null) =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath)
                .ConfigureTestServices(services => services.AddSingleton(clock ?? TimeProvider.System)))
            .CreateClient();
}
