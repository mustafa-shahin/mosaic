using System.Net;
using System.Text;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Tests;

public sealed class CacheControlTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // /health needs no database
    [Fact]
    public async Task AnAnswer_TellsEveryCacheNotToKeepIt()
    {
        var response = await factory.CreateClient().GetAsync(ApiPaths.Health, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ARefusalForLackOfASession_IsNotKeptEither()
    {
        var response = await factory.CreateClient().GetAsync(ApiPaths.Auth + AuthRoutes.Session, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // its own host, so the spent bucket leaves every other test alone
    [Fact]
    public async Task ARefusalForTooManyRequests_IsNotKeptEither()
    {
        var client = factory.WithWebHostBuilder(_ => { }).CreateClient();

        // the register bucket holds ten, and a body that cannot be read never reaches the database
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await client.PostAsync(ApiPaths.Auth + AuthRoutes.Register, Unreadable(), Token);
        }

        var refused = await client.PostAsync(ApiPaths.Auth + AuthRoutes.Register, Unreadable(), Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("no-store", refused.Headers.CacheControl?.ToString());
    }

    // the exception handler clears every header and writes its own no-cache, no-store
    [Fact]
    public async Task AnAnswerFromTheExceptionHandler_IsNotKeptEither()
    {
        var response = await factory.CreateClient().PostAsync(ApiPaths.Auth + AuthRoutes.SignIn, Unreadable(), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static StringContent Unreadable() => new("{", Encoding.UTF8, "application/json");
}
