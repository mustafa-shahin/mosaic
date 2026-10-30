using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests;

// through the real pipeline, where the authentication handler runs before the limiter
[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignedInRateLimitsTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the address's bucket would refuse the 101st, so a signed-in request is counted against its person
    [Fact]
    public async Task ASignedInPerson_GetsPastTheAddressBucket()
    {
        var client = Client();
        var token = await SignedIn();

        for (var request = 0; request < 150; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, ApiPaths.Auth + AuthRoutes.Session, token)).StatusCode);
        }
    }

    // attempts with a token use up the address's bucket, so the next one without a token is refused
    [Theory]
    [InlineData(ApiPaths.Auth + AuthRoutes.Register, 10)]
    [InlineData(ApiPaths.Auth + AuthRoutes.SignIn, 30)]
    public async Task StrictBuckets_CountAgainstTheAddressEvenWithAToken(string path, int bucket)
    {
        var client = Client();
        var token = await SignedIn();

        for (var attempt = 0; attempt < bucket; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, HttpMethod.Post, path, token)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, HttpMethod.Post, path, null)).StatusCode);
    }

    // written straight to the database, so signing in costs no request from either bucket
    private async Task<string> SignedIn()
    {
        var now = TimeProvider.System.GetUtcNow();
        var user = User.CreateOwner($"{Guid.CreateVersion7():N}@mosaic.test", "Ada", "Lovelace", "unused here", now);
        var (token, hash) = SessionTokenFormat.Create();

        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.Users.Add(user);
        context.SessionTokens.Add(SessionToken.Issue(user, hash, now));

        await context.SaveChangesAsync(Token);

        return token;
    }

    // a POST carries a body that cannot be read, so register and sign-in answer 400 before they hash anything
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent("{", Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request, Token);
    }

    // a host per test, so each starts with full buckets
    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();
}
