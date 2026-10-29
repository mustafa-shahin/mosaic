using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Tests.Identity.Sessions;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignOutEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SigningOut_Is200AndEndsThatSessionAtTheNextRequest()
    {
        var client = Client();
        var token = await SignIn(client, await Owner(client));

        var response = await Send(client, HttpMethod.Post, "/v1/auth/sign-out", token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body!.Success);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetSession(client, token)).StatusCode);
    }

    [Fact]
    public async Task SigningOut_LeavesThePersonsOtherSessionWorking()
    {
        var client = Client();
        var email = await Owner(client);
        var first = await SignIn(client, email);
        var second = await SignIn(client, email);

        await Send(client, HttpMethod.Post, "/v1/auth/sign-out", first);

        Assert.Equal(HttpStatusCode.OK, (await GetSession(client, second)).StatusCode);
    }

    // a dead token never reaches the sign-out code, so it gets the answer every endpoint gives it
    [Fact]
    public async Task SigningOutWithATokenThatOpensNoSession_Is401()
    {
        var client = Client();
        var token = await SignIn(client, await Owner(client));

        await Send(client, HttpMethod.Post, "/v1/auth/sign-out", token);

        var again = await Send(client, HttpMethod.Post, "/v1/auth/sign-out", token);

        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
        Assert.Equal("Bearer realm=\"mosaic\", error=\"invalid_token\"", Assert.Single(again.Headers.GetValues("WWW-Authenticate")));
    }

    [Fact]
    public async Task SigningOutEverywhere_EndsEverySessionIncludingTheOneThatAsked()
    {
        var client = Client();
        var email = await Owner(client);
        var first = await SignIn(client, email);
        var second = await SignIn(client, email);

        var response = await Send(client, HttpMethod.Post, "/v1/auth/sign-out-everywhere", first);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetSession(client, first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetSession(client, second)).StatusCode);
    }

    private async Task<string> Owner(HttpClient client)
    {
        await postgres.ResetAsync(Token);

        var email = $"{Guid.CreateVersion7():N}@mosaic.test";
        var request = new RegisterRequest
        {
            Email = email,
            FirstName = "Ada",
            LastName = "Lovelace",
            Password = Password,
            SetupToken = PostgresFixture.SetupToken,
        };

        var response = await client.PostAsJsonAsync("/v1/auth/register", request, MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return email;
    }

    private static async Task<string> SignIn(HttpClient client, string email)
    {
        var request = new SignInRequest { Email = email, Password = Password };

        var response = await client.PostAsJsonAsync("/v1/auth/sign-in", request, MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResult>>(MosaicJson.Options, Token);

        return body!.Data!.Token;
    }

    private static Task<HttpResponseMessage> GetSession(HttpClient client, string token) =>
        Send(client, HttpMethod.Get, "/v1/auth/session", token);

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client.SendAsync(request, Token);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();
}
