using System.Net;
using Mosaic.Contracts;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Health;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services.Health;

public sealed class HealthApiTests
{
    // a literal on purpose, and without a version: monitors and older desktops ask for exactly this
    [Fact]
    public async Task Get_AsksTheUnversionedHealthAddress()
    {
        var recorder = new RecordingHandler(
            HttpStatusCode.OK,
            $$"""{"data":{"service":"Mosaic.Api","serverTimeUtc":"2026-01-01T09:30:00+00:00","apiContract":"{{ApiContract.Version}}"},"success":true}""");

        var health = new HealthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        await health.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, recorder.Method);
        Assert.Equal("/health", recorder.Path);
    }
}
