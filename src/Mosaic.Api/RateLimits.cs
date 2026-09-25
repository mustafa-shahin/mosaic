using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Contracts;

namespace Mosaic.Api;

internal static partial class RateLimits
{
    public const string Register = "register";

    // a starting desktop sends a burst, and an office of desktops shares one address
    private const int AnonymousTokens = 100;
    private static readonly TimeSpan AnonymousRefill = TimeSpan.FromSeconds(1);

    private const int RegisterTokens = 10;
    private static readonly TimeSpan RegisterRefill = TimeSpan.FromMinutes(1);

    // a provider hands out a whole /64, so the full address would give one client unlimited buckets
    private const int IPv6PrefixBytes = 8;

    // a test host or a unix socket, limited together rather than not at all
    private const string NoAddress = "no-address";

    public static void Configure(RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetTokenBucketLimiter(ClientKey(context), _ => Bucket(AnonymousTokens, AnonymousRefill)));

        options.AddPolicy(Register, context =>
            RateLimitPartition.GetTokenBucketLimiter(ClientKey(context), _ => Bucket(RegisterTokens, RegisterRefill)));

        // the default is 503, which here means the server is busy, not this client
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = RejectAsync;
    }

    internal static string ClientKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;

        if (address is null)
        {
            return NoAddress;
        }

        // Kestrel reports an IPv4 client as ::ffff:a.b.c.d, cut to /64 that would put all of IPv4 in one bucket
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var prefix = address.GetAddressBytes();

        Array.Clear(prefix, IPv6PrefixBytes, prefix.Length - IPv6PrefixBytes);

        return new IPAddress(prefix).ToString();
    }

    // nothing queues: a refused request is answered at once
    private static TokenBucketRateLimiterOptions Bucket(int tokens, TimeSpan refill) => new()
    {
        TokenLimit = tokens,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = refill,
        QueueLimit = 0,
    };

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimits));

        LogRefusal(logger);

        var response = ApiResponse.Fail(new ApiError { Code = ErrorCodes.TooManyRequests }) with
        {
            TraceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        await httpContext.Response.WriteAsJsonAsync(response, MosaicJson.Options, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request refused with " + ErrorCodes.TooManyRequests)]
    private static partial void LogRefusal(ILogger logger);
}
