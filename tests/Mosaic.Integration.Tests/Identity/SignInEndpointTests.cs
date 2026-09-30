using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Security;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class SignInEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = "/v1/auth/sign-in";
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheRightPassword_Is200WithTheTokenOfTheStoredSession()
    {
        var client = Client();
        var email = await Owner(client);

        var response = await client.PostAsJsonAsync(Path, Request(email, Password), MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResult>>(MosaicJson.Options, Token);
        var result = body!.Data!;
        var session = await FindSession(result.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, result.Session.Email);
        Assert.Equal(result.Session.UserId, session.UserId);
    }

    // the token travels in the body, so no cache on its way may keep a copy
    [Fact]
    public async Task TheTokenResponse_TellsEveryCacheNotToKeepIt()
    {
        var client = Client();
        var email = await Owner(client);

        var response = await client.PostAsJsonAsync(Path, Request(email, Password), MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AWrongPassword_Is403WithTheGenericCode()
    {
        var client = Client();
        var email = await Owner(client);

        var response = await client.PostAsJsonAsync(Path, Request(email, "wrong horse battery staple"), MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, body!.Error!.Code);
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

    // found by the hash of the token, the way every signed-in request will find it
    private async Task<SessionToken> FindSession(string token)
    {
        var hash = SessionTokenFormat.HashOf(token);

        await using var scope = postgres.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ManagementDbContext>()
            .SessionTokens.SingleAsync(session => session.TokenHash == hash, Token);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();

    private static SignInRequest Request(string email, string password) => new() { Email = email, Password = password };
}
