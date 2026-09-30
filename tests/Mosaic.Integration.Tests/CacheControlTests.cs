using System.Net;

namespace Mosaic.Integration.Tests;

public sealed class CacheControlTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // /health needs no database, and every action gets the same filter
    [Fact]
    public async Task AnAction_TellsEveryCacheNotToKeepItsResponse()
    {
        var response = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
