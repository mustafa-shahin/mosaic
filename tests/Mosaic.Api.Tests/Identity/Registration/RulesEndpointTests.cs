using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Tests.Identity.Registration;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class RulesEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = ApiPaths.Auth + AuthRoutes.Rules;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // byte for byte what an installed desktop reads, so a renamed member or enum value fails here
    [Fact]
    public async Task WithoutAToken_AnUnclaimedInstanceAnswersTheRulesOnTheWire()
    {
        await postgres.ResetAsync(Token);

        var response = await Client().GetAsync(Path, Token);
        var json = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"data":{"registration":"Unclaimed","password":{"minLength":15,"maxLength":128}},"success":true}""", json);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OnceTheOwnerRegistered_TheInstanceIsClosed()
    {
        await postgres.ResetAsync(Token);

        var client = Client();
        var register = new RegisterRequest
        {
            Email = $"{Guid.CreateVersion7():N}@mosaic.test",
            FirstName = "Ada",
            LastName = "Lovelace",
            Password = "correct horse battery staple",
            SetupToken = PostgresFixture.SetupToken,
        };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiPaths.Auth + AuthRoutes.Register, register, MosaicJson.Options, Token)).StatusCode);

        var body = await client.GetFromJsonAsync<ApiResponse<RulesResult>>(Path, MosaicJson.Options, Token);

        Assert.Equal(RegistrationState.Closed, body!.Data!.Registration);
    }

    // every desktop asks on every start, so asking must never use up register's ten attempts
    [Fact]
    public async Task AskingTheRules_LeavesRegistersBucketAlone()
    {
        await postgres.ResetAsync(Token);

        var client = Client();

        for (var asked = 0; asked < 12; asked++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path, Token)).StatusCode);
        }

        var register = new RegisterRequest
        {
            Email = $"{Guid.CreateVersion7():N}@mosaic.test",
            FirstName = "Ada",
            LastName = "Lovelace",
            Password = "correct horse battery staple",
            SetupToken = PostgresFixture.SetupToken,
        };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiPaths.Auth + AuthRoutes.Register, register, MosaicJson.Options, Token)).StatusCode);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();
}
