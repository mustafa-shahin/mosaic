namespace Mosaic.Contracts;

// An installed desktop keeps sending what these spelled when it was built, so a change here breaks it.
public static class ApiVersions
{
    private const string Major1 = "1";

    // what a v1 controller declares in [ApiVersion]
    public const string V1 = Major1 + ".0";

    // what a client writes in front of a v1 path
    public const string V1Path = "v" + Major1;

    // the start of every versioned route, where Asp.Versioning reads the version from
    public const string RouteTemplate = "v{version:apiVersion}";
}
