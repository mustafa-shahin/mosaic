using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Mosaic.Api;
using Mosaic.Contracts;

namespace Mosaic.Integration.Tests;

public sealed class RateLimitsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::")]
    public void TheKey_IsTheAddressWithIPv6CutToItsSlash64(string address, string key)
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        Assert.Equal(key, RateLimits.ClientKey(context));
    }

    [Fact]
    public void RequestsWithNoAddress_ShareOneKey()
    {
        Assert.Equal(RateLimits.ClientKey(new DefaultHttpContext()), RateLimits.ClientKey(new DefaultHttpContext()));
    }

    [Fact]
    public async Task OneAddress_GetsAHundredRequestsAtOnceAndThenA429InTheEnvelope()
    {
        var client = NewHost();

        for (var request = 0; request < 100; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Token)).StatusCode);
        }

        var refused = await client.GetAsync("/health", Token);
        var body = await refused.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("1", Assert.Single(refused.Headers.GetValues("Retry-After")));
        Assert.Equal(ErrorCodes.TooManyRequests, body!.Error!.Code);
        Assert.NotNull(body.TraceId);
    }

    // a body that cannot be read is refused before the action, so no request here needs a database
    [Fact]
    public async Task Register_AllowsTenAttemptsAndLeavesOtherEndpointsAlone()
    {
        var client = NewHost();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/v1/auth/register", Unreadable(), Token)).StatusCode);
        }

        var refused = await client.PostAsync("/v1/auth/register", Unreadable(), Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("60", Assert.Single(refused.Headers.GetValues("Retry-After")));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Token)).StatusCode);
    }

    [Fact]
    public async Task SignIn_AllowsThirtyAttemptsAndThenAsksForSixSeconds()
    {
        var client = NewHost();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/v1/auth/sign-in", Unreadable(), Token)).StatusCode);
        }

        var refused = await client.PostAsync("/v1/auth/sign-in", Unreadable(), Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("6", Assert.Single(refused.Headers.GetValues("Retry-After")));
    }

    // each host has its own buckets, so no test spends another's
    private HttpClient NewHost() => factory.WithWebHostBuilder(_ => { }).CreateClient();

    private static StringContent Unreadable() => new("{", Encoding.UTF8, "application/json");
}
