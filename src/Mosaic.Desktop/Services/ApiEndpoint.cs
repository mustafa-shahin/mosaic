namespace Mosaic.Desktop.Services;

// One place that knows where the API is.
public static class ApiEndpoint
{
    private const string OverrideVariable = "MOSAIC_API_URL";
    private const string DevelopmentDefault = "http://localhost:5175/";

    public static Uri? FromEnvironment() => Resolve(Environment.GetEnvironmentVariable(OverrideVariable));

    // Null for an unusable value, since a typo in the variable must neither crash the app nor send it to another server.
    public static Uri? Resolve(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? DevelopmentDefault : configured;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var address))
        {
            return null;
        }

        // Uri accepts any syntactically valid scheme, so "localhost:5175" parses with scheme "localhost".
        if (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // A query or fragment on the address is dropped when a relative path is combined, so either one is refused.
        if (address.Query.Length > 0 || address.Fragment.Length > 0)
        {
            return null;
        }

        // Without a trailing slash the last path segment is dropped when a relative path is combined.
        return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
    }
}
