using Microsoft.AspNetCore.HttpOverrides;

// HttpOverrides has its own IPNetwork, the one the obsolete KnownNetworks takes
using IPNetwork = System.Net.IPNetwork;

namespace Mosaic.Api;

internal static class TrustedProxies
{
    public const string Key = "ForwardedHeaders:TrustedNetworks";

    // nothing sits in front of the API, said on purpose
    private const string None = "none";

    // null keeps the framework default, loopback only, empty trusts nobody
    public static IReadOnlyList<IPNetwork>? Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[Key];

        if (configured is null)
        {
            return environment.IsDevelopment()
                ? null
                : throw new InvalidOperationException(
                    $"{Key} is not configured. Set it to the CIDR range of the network the API shares with its "
                    + $"reverse proxy, several separated by commas, or to \"{None}\" when nothing sits in front of it. "
                    + "Without it every client address is the proxy's, in the rate limiter and in the audit trail.");
        }

        if (configured.Trim().Equals(None, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Parse)
            .ToList();
    }

    public static void Apply(ForwardedHeadersOptions options, IReadOnlyList<IPNetwork>? networks)
    {
        // with empty trust lists the middleware believes every peer, so trusting nobody means reading no header
        if (networks is { Count: 0 })
        {
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

        if (networks is null)
        {
            return;
        }

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    private static IPNetwork Parse(string value) =>
        IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException(
                $"{Key} contains \"{value}\", which is not a CIDR range such as 172.20.0.0/16.");
}
