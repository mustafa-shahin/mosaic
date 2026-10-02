using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Security;
using Mosaic.Contracts;

namespace Mosaic.Integration.Tests;

// Checks the handler is actually wired into the pipeline and wins over the ProblemDetails fallback.
public sealed class ExceptionPipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    // authentication runs after the exception handler, so a session check that cannot reach its database still answers in the envelope
    [Fact]
    public async Task ASessionCheckThatThrows_AnswersWithTheEnvelopeAsJson()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/session");

        // well formed, so the check queries the database this factory never reaches
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionTokenFormat.Create().Token);

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ErrorCodes.Unexpected, body!.Error!.Code);
    }

    private sealed class UnavailableClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("clock unavailable");
    }
}
