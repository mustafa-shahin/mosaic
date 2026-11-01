using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Mosaic.Contracts;

namespace Mosaic.Api.Tests;

public sealed class ShippedRoutesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // written out by hand: an installed desktop keeps sending these, so changing one needs a new API version
    private static readonly string[] Shipped =
    [
        "GET /health",
        "GET /v1/auth/rules",
        "POST /v1/auth/register",
        "POST /v1/auth/sign-in",
        "GET /v1/auth/session",
        "POST /v1/auth/sign-out",
        "POST /v1/auth/sign-out-everywhere",
    ];

    private static readonly string VersionParameter = RoutePatternFactory.Parse(ApiVersions.RouteTemplate).Parameters.Single().Name;

    [Fact]
    public void EveryShippedRoute_IsStillMapped() =>
        Assert.Empty(Shipped.Except(Mapped()));

    [Fact]
    public void EveryMappedRoute_IsWrittenOutHere() =>
        Assert.Empty(Mapped().Except(Shipped));

    private IEnumerable<string> Mapped() =>
        Endpoints.Of(factory).SelectMany(pair => AsSent(pair.Endpoint));

    private static IEnumerable<string> AsSent(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        var declared = endpoint.Metadata.GetMetadata<ApiVersionMetadata>()?
            .Map(ApiVersionMapping.Explicit | ApiVersionMapping.Implicit).DeclaredApiVersions ?? [];
        IEnumerable<ApiVersion?> versions = declared.Count == 0 ? [null] : [.. declared];

        return methods.SelectMany(method => versions.Select(version => $"{method} {PathFor(endpoint.RoutePattern, version)}"));
    }

    // a client fills the version in as its major number, so the group's declared 1.0 reads v1
    private static string PathFor(RoutePattern pattern, ApiVersion? version) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart parameter when parameter.Name == VersionParameter && version is not null =>
                version.MajorVersion?.ToString(CultureInfo.InvariantCulture),
            RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
            _ => part.ToString(),
        }))));
}
