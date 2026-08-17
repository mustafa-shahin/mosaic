using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Mosaic.Contracts;

namespace Mosaic.Integration.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsSuccessEnvelopeWithTheServiceName()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

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

        var json = await client.GetStringAsync("/health", TestContext.Current.CancellationToken);

        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"service\":\"Mosaic.Api\"", json);
        Assert.Contains($"\"apiContract\":\"{ApiContract.Version}\"", json);
        Assert.DoesNotContain("\"error\"", json);
        Assert.DoesNotContain("\"traceId\"", json);
    }
}
