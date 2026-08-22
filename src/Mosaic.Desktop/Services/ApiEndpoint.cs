using System.Net;
using System.Net.Sockets;

namespace Mosaic.Desktop.Services;

// One place that knows where the API is.
public static class ApiEndpoint
{
    private const string OverrideVariable = "MOSAIC_API_URL";
    private const string DevelopmentDefault = "http://localhost:5175/";

    public static Uri? FromEnvironment() => Resolve(Environment.GetEnvironmentVariable(OverrideVariable));

    // Null means the configured value cannot be used. A typo in an environment variable is a state
    // the app has to survive, so this never throws and never falls back to a different server.
    public static Uri? Resolve(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? DevelopmentDefault : configured;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var address))
            return null;


        // Uri accepts any syntactically valid scheme, so "localhost:5175" parses with scheme "localhost".
        if (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
            return null;
        

        // A base address is a starting point, not a request. A query or fragment on it is a mistake.
        if (address.Query.Length > 0 || address.Fragment.Length > 0)
            return null;
        

        // Without a trailing slash the last path segment is dropped when a relative path is combined.
        return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
    }
}

