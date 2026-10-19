using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mosaic.Api.Authorization;
using Mosaic.Api.Controllers.Health;
using Mosaic.Api.Controllers.Identity;

namespace Mosaic.Integration.Tests;

public sealed class AccessDeclarationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // a rule attribute missing from this list makes every action that uses it fail as undeclared
    private static readonly Type[] AccessRules = [typeof(AllowAnonymousAttribute), typeof(AllowAnySignedInAttribute)];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // a forgotten rule fails here, instead of leaving the action open to every signed-in person
    [Fact]
    public void EveryAction_DeclaresExactlyOneAccessRule()
    {
        var undeclared = new List<string>();

        foreach (var action in Actions())
        {
            var declared = action.GetCustomAttributes().Where(IsAccessRule).ToList();

            if (declared.Count != 1 || !AccessRules.Contains(declared[0].GetType()))
            {
                undeclared.Add($"{action.DeclaringType!.Name}.{action.Name}");
            }
        }

        Assert.Empty(undeclared);
    }

    // health and the rules cost no hash and take no input to guess, so the address's bucket is their only limit
    [Fact]
    public void EveryAnonymousAction_NamesAStrictLimit()
    {
        var unlimited = new List<string>();

        foreach (var action in Actions())
        {
            if (action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                && action.GetCustomAttribute<EnableRateLimitingAttribute>() is null
                && action.DeclaringType != typeof(HealthController)
                && !(action.DeclaringType == typeof(AuthController) && action.Name == nameof(AuthController.Rules)))
            {
                unlimited.Add($"{action.DeclaringType!.Name}.{action.Name}");
            }
        }

        Assert.Empty(unlimited);
    }

    // [AllowAnonymous] on a controller wins over every rule its actions declare
    [Fact]
    public void NoController_DeclaresAnAccessRule()
    {
        var declaring = Controllers()
            .Where(controller => controller.GetCustomAttributes().Any(IsAccessRule))
            .Select(controller => controller.Name);

        Assert.Empty(declaring);
    }

    // only the fallback policy closes a path that no action declares
    [Fact]
    public async Task APathNoActionDeclares_Is401WithoutAToken()
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

    private static IEnumerable<Type> Controllers() =>
        typeof(Program).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ControllerBase)));

    private static IEnumerable<MethodInfo> Actions() =>
        Controllers().SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
}
