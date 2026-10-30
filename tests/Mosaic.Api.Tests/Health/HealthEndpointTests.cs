using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts;
using Mosaic.Contracts.Health;

namespace Mosaic.Api.Tests.Health;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ReturnsSuccessEnvelopeWithTheServiceName()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(ApiPaths.Health, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HealthResult>>(
            MosaicJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Null(body.Error);
        Assert.Equal("Mosaic.Api", body.Data!.Service);
        Assert.Equal(ApiContract.Version, body.Data.ApiContract);
    }

    [Fact]
    public async Task Health_OnTheWire_IsCamelCaseAndOmitsNullMembers()
    {
        var client = factory.CreateClient();

        var json = await client.GetStringAsync(ApiPaths.Health, TestContext.Current.CancellationToken);

        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"service\":\"Mosaic.Api\"", json);
        Assert.Contains($"\"apiContract\":\"{ApiContract.Version}\"", json);
        Assert.DoesNotContain("\"error\"", json);
        Assert.DoesNotContain("\"traceId\"", json);
    }

    // a signed-in desktop sends its token here too, and checking it would need the database this factory never reaches
    [Fact]
    public async Task Health_WithATokenWhoseCheckCannotReachTheDatabase_StillAnswers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiPaths.Health);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionTokenFormat.Create().Token);

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
