using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Mosaic.Api;

namespace Mosaic.Integration.Tests;

public sealed class TrustedProxiesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TrustedNetwork = "172.20.0.0/16";
    private const string Proxy = "172.20.0.5";
    private const string Neighbour = "172.20.0.9";
    private const string Outsider = "198.51.100.9";
    private const string Client = "203.0.113.7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void OutsideDevelopmentWithNothingConfigured_TheApiRefusesToStart()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => StartInProduction(configured: null));

        Assert.Contains(TrustedProxies.Key, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueThatIsNotACidrRange_RefusesToStart()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => StartInProduction("172.20.0.0"));

        Assert.Contains("172.20.0.0", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWordNone_Starts()
    {
        using var client = StartInProduction("none");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Token)).StatusCode);
    }

    [Fact]
    public async Task ACidrRange_Starts()
    {
        using var client = StartInProduction("172.20.0.0/16, 10.0.0.0/8");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Token)).StatusCode);
    }

    [Fact]
    public async Task BehindTheProxy_EachForwardedAddressGetsItsOwnBucket()
    {
        var server = BehindTheProxy();

        await SpendTheBucketAsync(server, Proxy, _ => Client);

        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendAsync(server, Proxy, Client));
        Assert.Equal(StatusCodes.Status200OK, await SendAsync(server, Proxy, "203.0.113.8"));
    }

    [Fact]
    public async Task APeerOutsideTheNetwork_IsLimitedByItsOwnAddressWhateverItForwards()
    {
        var server = BehindTheProxy();

        await SpendTheBucketAsync(server, Outsider, request => $"203.0.113.{request}");

        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendAsync(server, Outsider, "203.0.113.200"));
    }

    [Fact]
    public async Task WithNone_NoPeerIsBelieved()
    {
        var server = factory.WithWebHostBuilder(builder => builder.UseSetting(TrustedProxies.Key, "none")).Server;

        await SpendTheBucketAsync(server, Outsider, request => $"203.0.113.{request}");

        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendAsync(server, Outsider, "203.0.113.200"));
    }

    // a second entry is read only when the first is inside the network, so the client here sits inside it
    [Fact]
    public async Task OnlyTheEntryTheProxyAppended_IsRead()
    {
        var server = BehindTheProxy();

        await SpendTheBucketAsync(server, Proxy, request => $"10.9.0.{request}, {Neighbour}");

        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendAsync(server, Proxy, $"10.9.0.200, {Neighbour}"));
    }

    // CreateClient builds the host, so a refusal to start surfaces here
    private HttpClient StartInProduction(string? configured) => factory
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");

            if (configured is not null)
            {
                builder.UseSetting(TrustedProxies.Key, configured);
            }
        })
        .CreateClient();

    private TestServer BehindTheProxy() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting(TrustedProxies.Key, TrustedNetwork)).Server;

    // the global bucket holds a hundred, so the next request with the same key is refused
    private static async Task SpendTheBucketAsync(TestServer server, string peer, Func<int, string> forwardedFor)
    {
        for (var request = 0; request < 100; request++)
        {
            Assert.Equal(StatusCodes.Status200OK, await SendAsync(server, peer, forwardedFor(request)));
        }
    }

    private static async Task<int> SendAsync(TestServer server, string peer, string forwardedFor)
    {
        var context = await server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/health";
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }, Token);

        return context.Response.StatusCode;
    }
}
