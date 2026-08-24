using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Contracts;

namespace Mosaic.Integration.Tests;

// Checks the handler is actually wired into the pipeline and wins over the ProblemDetails fallback.
public sealed class ExceptionPipelineTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Endpoint_WhenItThrows_AnswersWithTheEnvelopeAsJson()
    {
        var client = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<TimeProvider>(new UnavailableClock())))
            .CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(
            MosaicJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.Unexpected, body.Error!.Code);
    }

    private sealed class UnavailableClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("clock unavailable");
    }
}
