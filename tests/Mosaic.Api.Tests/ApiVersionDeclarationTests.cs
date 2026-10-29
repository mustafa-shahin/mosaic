using Asp.Versioning;
using Mosaic.Api.Health;
using Mosaic.Contracts;

namespace Mosaic.Api.Tests;

public sealed class ApiVersionDeclarationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly ApiVersion V1 = ApiVersionParser.Default.Parse(ApiVersions.V1);

    // an endpoint outside the versioned groups declares no version, so nothing keeps it in v1 when v2 arrives
    [Fact]
    public void EveryEndpointButHealth_ServesTheVersionItsGroupDeclares()
    {
        var health = typeof(HealthEndpoints).GetMethod(nameof(HealthEndpoints.Get));

        var undeclared = Endpoints.Of(factory)
            .Where(pair => pair.Method != health
                && pair.Endpoint.Metadata.GetMetadata<ApiVersionMetadata>()?.IsMappedTo(V1) != true)
            .Select(pair => Endpoints.Describe(pair.Endpoint, pair.Method));

        Assert.Empty(undeclared);
    }

    // a monitor and an older desktop reach it whatever version the server speaks
    [Fact]
    public void Health_ServesNoVersion()
    {
        var health = typeof(HealthEndpoints).GetMethod(nameof(HealthEndpoints.Get));

        var (endpoint, _) = Assert.Single(Endpoints.Of(factory), pair => pair.Method == health);
        var metadata = endpoint.Metadata.GetMetadata<ApiVersionMetadata>();

        Assert.True(metadata is null || metadata.IsApiVersionNeutral);
    }
}
