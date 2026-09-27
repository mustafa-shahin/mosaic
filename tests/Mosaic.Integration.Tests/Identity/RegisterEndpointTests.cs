using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class RegisterEndpointTests(PostgresFixture postgres, ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = "/v1/auth/register";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheFirstRegistration_Is201WithTheOutcomeInTheEnvelope()
    {
        await postgres.ResetAsync(Token);

        var response = await Client().PostAsJsonAsync(Path, Request(UniqueEmail()), MosaicJson.Options, Token);
        var json = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"outcome\":\"Owner\"", json);
    }

    [Fact]
    public async Task UnderClosed_ALaterRegistrationIs403WithItsCode()
    {
        await postgres.ResetAsync(Token);

        var client = Client();

        await client.PostAsJsonAsync(Path, Request(UniqueEmail()), MosaicJson.Options, Token);

        var response = await client.PostAsJsonAsync(Path, Request(UniqueEmail()), MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.RegistrationClosed, body!.Error!.Code);
    }

    // only an unclaimed instance reads the request, a closed one refuses before the rules run
    [Fact]
    public async Task ARequestThatFailsTheRules_Is400WithEachFieldsCode()
    {
        await postgres.ResetAsync(Token);

        var request = new { FirstName = "Ada", LastName = "Lovelace", Password = "short", PostgresFixture.SetupToken };
        var response = await Client().PostAsJsonAsync(Path, request, MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);
        var fields = body!.Error!.FieldErrors!;
        var password = Assert.Single(fields["password"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error.Code);
        Assert.Equal(2, fields.Count);
        Assert.Equal(ErrorCodes.Required, Assert.Single(fields["email"]).Code);
        Assert.Equal(ErrorCodes.TooShort, password.Code);
        Assert.Equal("15", password.Params![ErrorParams.Limit]);
    }

    // the desktop shows the token field when it reads this code, so the code must reach it in the envelope
    [Fact]
    public async Task WithoutAToken_AnUnclaimedInstanceIs403WithItsCode()
    {
        await postgres.ResetAsync(Token);

        var request = Request(UniqueEmail()) with { SetupToken = null };
        var response = await Client().PostAsJsonAsync(Path, request, MosaicJson.Options, Token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.SetupTokenRequired, body!.Error!.Code);
    }

    private HttpClient Client() =>
        factory.WithWebHostBuilder(builder => builder
                .UseSetting("ConnectionStrings:Management", postgres.ConnectionString)
                .UseSetting("Setup:TokenPath", postgres.SetupTokenPath))
            .CreateClient();

    private static RegisterRequest Request(string email) => new()
    {
        Email = email,
        FirstName = "Ada",
        LastName = "Lovelace",
        Password = "correct horse battery staple",
        SetupToken = PostgresFixture.SetupToken,
    };

    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";
}
