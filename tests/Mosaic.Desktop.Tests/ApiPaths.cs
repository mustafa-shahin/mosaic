using Mosaic.Contracts;
using Mosaic.Contracts.Health;
using Mosaic.Contracts.Identity;

namespace Mosaic.Desktop.Tests;

// the paths built from the route constants; only AuthApiTests and HealthApiTests spell them out by hand
internal static class ApiPaths
{
    // relative to the server's address, as a typed client hands it to ApiClient
    public const string RelativeAuth = ApiVersions.V1Path + "/" + AuthRoutes.Prefix + "/";

    // where such a request lands, as a recording handler reads it from the address
    public const string Auth = "/" + RelativeAuth;

    public const string Health = "/" + HealthRoutes.Health;
}
