using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Contracts.Health;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Tests;

// the paths as a client sends them, built from the route constants; only ShippedRoutesTests spells the routes out
internal static class ApiPaths
{
    public const string Health = "/" + HealthRoutes.Health;

    public const string Auth = "/" + ApiVersions.V1Path + "/" + AuthRoutes.Prefix + "/";

    public const string Audit = "/" + ApiVersions.V1Path + "/" + AuditRoutes.Prefix + "/";
}
