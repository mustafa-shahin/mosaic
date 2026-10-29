using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Contracts;

namespace Mosaic.Api.Tests;

public sealed class RateLimitsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly IPAddress Office = IPAddress.Parse("203.0.113.7");

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
    public void ASignedInRequest_IsKeyedOnItsPerson_AndAnAnonymousOneOnNothing()
    {
        var userId = Guid.CreateVersion7();

        Assert.Equal($"person:{userId}", RateLimits.PersonKey(SignedIn(userId, Office)));
        Assert.Null(RateLimits.PersonKey(Anonymous(Office)));
    }

    // straight through the limiter, so no refill can land between the 200th and the 201st
    [Fact]
    public void ASignedInPerson_Gets200AtOnceAndOneBackEvery200Milliseconds()
    {
        var options = new RateLimiterOptions();

        RateLimits.Configure(options);

        using var global = options.GlobalLimiter!;
        var person = SignedIn(Guid.CreateVersion7(), Office);

        for (var request = 0; request < 200; request++)
        {
            using var lease = global.AttemptAcquire(person);

            Assert.True(lease.IsAcquired);
        }

        using var refused = global.AttemptAcquire(person);

        Assert.False(refused.IsAcquired);
        Assert.True(refused.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, TimeSpan.FromTicks(1), TimeSpan.FromMilliseconds(200));
    }

    // an office behind one address shares its anonymous bucket, but each person signed in there has their own
    [Fact]
    public void ASignedInPerson_DoesNotShareTheAddressBucket()
    {
        var options = new RateLimiterOptions();

        RateLimits.Configure(options);

        using var global = options.GlobalLimiter!;

        for (var request = 0; request < 100; request++)
        {
            using var lease = global.AttemptAcquire(Anonymous(Office));

            Assert.True(lease.IsAcquired);
        }

        using var anonymous = global.AttemptAcquire(Anonymous(Office));
        using var signedIn = global.AttemptAcquire(SignedIn(Guid.CreateVersion7(), Office));

        Assert.False(anonymous.IsAcquired);
        Assert.True(signedIn.IsAcquired);
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

    // the limiter runs before authorization, so a refused request costs a token like any other
    [Fact]
    public async Task RequestsRefusedForLackOfASession_AreCountedToo()
    {
        var client = NewHost();

        for (var request = 0; request < 100; request++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/auth/session", Token)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/v1/auth/session", Token)).StatusCode);
    }

    private static DefaultHttpContext Anonymous(IPAddress address)
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = address;

        return context;
    }

    // only the NameIdentifier claim PersonKey reads, with no session or database behind it
    private static DefaultHttpContext SignedIn(Guid userId, IPAddress address)
    {
        var context = Anonymous(address);

        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

        return context;
    }

    // each host has its own buckets, so no test spends another's
    private HttpClient NewHost() => factory.WithWebHostBuilder(_ => { }).CreateClient();

    private static StringContent Unreadable() => new("{", Encoding.UTF8, "application/json");
}
