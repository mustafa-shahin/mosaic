using Asp.Versioning;
using Mosaic.Api.Identity.Registration;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Api.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentity(this IEndpointRouteBuilder endpoints)
    {
        // one group for both sub-features, so their routes share the prefix and the version is declared once
        var auth = endpoints.NewVersionedApi()
            .MapGroup(ApiVersions.RouteTemplate + "/" + AuthRoutes.Prefix)
            .HasApiVersion(ApiVersionParser.Default.Parse(ApiVersions.V1));

        auth.MapRegistration();
        auth.MapSessions();

        return endpoints;
    }
}
