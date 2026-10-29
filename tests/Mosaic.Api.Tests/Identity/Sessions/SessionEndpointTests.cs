using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Sessions;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SessionEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = "/v1/auth/session";
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheTokenFromSignIn_OpensTheSession()
    {
        var client = Client();
        var signedIn = await SignIn(client);

        var response = await Get(client, signedIn.Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SessionResult>>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(signedIn.Session, body!.Data);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // nothing may remember the person between requests, or a rotated stamp would end the session only later
    [Fact]
    public async Task OnceTheStampRotates_TheNextRequestIsRefused()
    {
        var client = Client();
        var signedIn = await SignIn(client);

        Assert.Equal(HttpStatusCode.OK, (await Get(client, signedIn.Token)).StatusCode);

        await using (var scope = postgres.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Id == signedIn.Session.UserId, Token);

            user.EndAllSessions();

            await context.SaveChangesAsync(Token);
        }

        var refused = await Get(client, signedIn.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal("Bearer realm=\"mosaic\", error=\"invalid_token\"", Assert.Single(refused.Headers.GetValues("WWW-Authenticate")));
    }

    private async Task<SignInResult> SignIn(HttpClient client)
    {
        await postgres.ResetAsync(Token);

        var email = $"{Guid.CreateVersion7():N}@mosaic.test";
        var register = new RegisterRequest
        {
            Email = email,
            FirstName = "Ada",
            LastName = "Lovelace",
            Password = Password,
            SetupToken = PostgresFixture.SetupToken,
        };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/v1/auth/register", register, MosaicJson.Options, Token)).StatusCode);

        var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new SignInRequest { Email = email, Password = Password }, MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResult>>(MosaicJson.Options, Token);

        return body!.Data!;
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client.SendAsync(request, Token);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();
}
