using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mosaic.Api.Tests;

// what the running API maps, read from routing itself, so an endpoint added anywhere is checked
internal static class Endpoints
{
    // the framework's own, such as the API description, are left out; an endpoint with no method is kept and fails
    public static IReadOnlyList<(RouteEndpoint Endpoint, MethodInfo? Method)> Of(ApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (endpoint, endpoint.Metadata.GetMetadata<MethodInfo>()))
            .Where(pair => pair.Item2 is null || pair.Item2.DeclaringType?.Assembly == typeof(Program).Assembly)
            .ToList();

    // a lambda compiles to a method with a generated name such as <<Main>$>b__0_1, which says nothing about its route
    public static bool IsNamed(MethodInfo method) =>
        method.GetCustomAttribute<CompilerGeneratedAttribute>() is null
        && method.DeclaringType?.GetCustomAttribute<CompilerGeneratedAttribute>() is null;

    public static string Describe(RouteEndpoint endpoint, MethodInfo? method) =>
        $"{endpoint.RoutePattern.RawText} -> {method?.DeclaringType?.Name}.{method?.Name}";
}
