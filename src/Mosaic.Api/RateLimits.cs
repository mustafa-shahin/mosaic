using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Contracts;

namespace Mosaic.Api;

internal static partial class RateLimits
{
    public const string Register = "register";
    public const string SignIn = "sign-in";
    public const string Rotate = "rotate";

    // a starting desktop sends a burst, and an office of desktops shares one address
    private const int AnonymousTokens = 100;
    private static readonly TimeSpan AnonymousRefill = TimeSpan.FromSeconds(1);

    // a person's open views poll every second or two, and the web UI and the MCP server use the same bucket
    private const int PersonTokens = 200;
    private static readonly TimeSpan PersonRefill = TimeSpan.FromMilliseconds(200);

    private const int RegisterTokens = 10;
    private static readonly TimeSpan RegisterRefill = TimeSpan.FromMinutes(1);

    // fifteen people behind one address signing in at once with a typo each, then 100 every 10 minutes
    private const int SignInTokens = 30;
    private static readonly TimeSpan SignInRefill = TimeSpan.FromSeconds(6);

    // every rotation leaves a row for up to 30 days, so a client stuck in a loop is held to about 10 an hour
    private const int RotateTokens = 10;
    private static readonly TimeSpan RotateRefill = TimeSpan.FromMinutes(6);

    // a provider hands out a whole /64, so the full address would give one client unlimited buckets
    private const int IPv6PrefixBytes = 8;

    // a test host or a unix socket, limited together rather than not at all
    private const string NoAddress = "no-address";

    private const string PersonKeyPrefix = "person:";

    public static void Configure(RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            PersonKey(context) is { } person
                ? RateLimitPartition.GetTokenBucketLimiter(person, _ => Bucket(PersonTokens, PersonRefill))
                : RateLimitPartition.GetTokenBucketLimiter(ClientKey(context), _ => Bucket(AnonymousTokens, AnonymousRefill)));

        // keyed on the address even with a token, so holding an account buys no fresh bucket of hashes
        options.AddPolicy(Register, context =>
            RateLimitPartition.GetTokenBucketLimiter(ClientKey(context), _ => Bucket(RegisterTokens, RegisterRefill)));

        options.AddPolicy(SignIn, context =>
            RateLimitPartition.GetTokenBucketLimiter(ClientKey(context), _ => Bucket(SignInTokens, SignInRefill)));

        // keyed on the person, so all of one person's clients share the ten
        options.AddPolicy(Rotate, context =>
            RateLimitPartition.GetTokenBucketLimiter(PersonKey(context) ?? ClientKey(context), _ => Bucket(RotateTokens, RotateRefill)));

        // the default is 503, which here means the server is busy, not this client
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = RejectAsync;
    }

    // UseAuthentication runs before the limiter, so a signed-in request already carries its person here
    internal static string? PersonKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId ? PersonKeyPrefix + userId : null;

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
