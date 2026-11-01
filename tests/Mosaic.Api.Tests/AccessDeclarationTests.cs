using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Api.Authorization;
using Mosaic.Api.Health;
using Mosaic.Api.Identity.Registration;

namespace Mosaic.Api.Tests;

public sealed class AccessDeclarationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // a rule attribute missing from this list makes every endpoint that uses it fail as undeclared
    private static readonly Type[] AccessRules = [typeof(AllowAnonymousAttribute), typeof(AllowAnySignedInAttribute)];

    // health and the rules cost no hash and take no input to guess, so the address's bucket is their only limit
    private static readonly MethodInfo[] AddressBucketOnly =
    [
        typeof(HealthEndpoints).GetMethod(nameof(HealthEndpoints.Get))!,
        typeof(RegistrationEndpoints).GetMethod(nameof(RegistrationEndpoints.Rules))!,
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void EveryEndpoint_PointsAtANamedMethod()
    {
        var unnamed = Endpoints.Of(factory)
            .Where(pair => pair.Method is null || !Endpoints.IsNamed(pair.Method))
            .Select(pair => Endpoints.Describe(pair.Endpoint, pair.Method));

        Assert.Empty(unnamed);
    }

    // a forgotten rule fails here, instead of leaving the endpoint open to every signed-in person
    [Fact]
    public void EveryEndpoint_DeclaresExactlyOneAccessRuleOnItsMethod()
    {
        var undeclared = new List<string>();

        foreach (var (endpoint, method) in Endpoints.Of(factory))
        {
            var declared = method?.GetCustomAttributes().Where(IsAccessRule).ToList() ?? [];

            if (declared.Count != 1 || !AccessRules.Contains(declared[0].GetType()))
            {
                undeclared.Add(Endpoints.Describe(endpoint, method));
            }
        }

        Assert.Empty(undeclared);
    }

    // a rule or a limit from a route group or a fluent call changes every endpoint below it, unseen on the method
    [Fact]
    public void EveryEndpoint_CarriesOnlyTheRulesAndLimitsItsMethodDeclares()
    {
        var added = new List<string>();

        foreach (var (endpoint, method) in Endpoints.Of(factory))
        {
            var declared = method?.GetCustomAttributes().Where(IsMarker).Select(Name).Order() ?? Enumerable.Empty<string>();
            var carried = endpoint.Metadata.Where(IsMarker).Select(Name).Order();

            if (!declared.SequenceEqual(carried))
            {
                added.Add($"{Endpoints.Describe(endpoint, method)}: {string.Join(", ", carried)}");
            }
        }

        Assert.Empty(added);
    }

    [Fact]
    public void EveryAnonymousEndpoint_NamesAStrictLimit()
    {
        var unlimited = Endpoints.Of(factory)
            .Where(pair => pair.Method?.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                && pair.Method.GetCustomAttribute<EnableRateLimitingAttribute>() is null
                && !AddressBucketOnly.Contains(pair.Method))
            .Select(pair => Endpoints.Describe(pair.Endpoint, pair.Method));

        Assert.Empty(unlimited);
    }

    // only the fallback policy closes a path that no endpoint declares
    [Fact]
    public async Task APathNoEndpointDeclares_Is401WithoutAToken()
    {
        var response = await factory.CreateClient().GetAsync("/no-such-path", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheApiDescriptionInDevelopment_NeedsNoToken()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static bool IsAccessRule(Attribute attribute) => attribute is IAllowAnonymous or IAuthorizeData;

    private static bool IsMarker(object item) =>
        item is IAllowAnonymous or IAuthorizeData or AuthorizationPolicy or EnableRateLimitingAttribute or DisableRateLimitingAttribute;

    private static string Name(object marker) =>
        marker is EnableRateLimitingAttribute limit ? $"{marker.GetType().Name}({limit.PolicyName})" : marker.GetType().Name;
}
