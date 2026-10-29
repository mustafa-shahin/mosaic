using System.Reflection;
using System.Security.Claims;
using Mosaic.Api.Identity.Sessions;

namespace Mosaic.Api.Tests;

// the host's commands and the job runner call use cases with no request, so a use case must never need one
public sealed class UseCaseDriftTests
{
    private const string AspNetCore = "Microsoft.AspNetCore";

    private static readonly string OwnPrefix = nameof(Mosaic) + ".";

    // the one public method every use case has, and nothing else has
    private static readonly string Handle = nameof(SignInUser.HandleAsync);

    [Fact]
    public void NoUseCase_ReachesAnAspNetTypeOrAClaimsPrincipal()
    {
        var ours = OurAssemblies();
        var useCases = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetMethod(Handle, BindingFlags.Public | BindingFlags.Instance) is not null)
            .ToList();

        Assert.NotEmpty(useCases);

        var chains = new List<string>();

        foreach (var useCase in useCases)
        {
            foreach (var parameter in useCase.GetMethod(Handle)!.GetParameters())
            {
                Follow(parameter.ParameterType, [useCase], ours, chains);
            }

            FollowConstructors(useCase, [useCase], ours, chains);
        }

        Assert.True(chains.Count == 0, string.Join(Environment.NewLine, chains.Distinct()));
    }

    private static void Follow(Type type, List<Type> path, Assembly[] ours, List<string> chains)
    {
        // ILogger<SetupTokenFile> and IEnumerable<IPermissionSource> are walked for what they carry
        foreach (var carried in type.HasElementType ? [type.GetElementType()!] : type.GetGenericArguments())
        {
            Follow(carried, path, ours, chains);
        }

        if (type.Namespace?.StartsWith(AspNetCore, StringComparison.Ordinal) == true
            || type.IsAssignableTo(typeof(ClaimsPrincipal)))
        {
            chains.Add(string.Join(" → ", path.Append(type).Select(step => step.Name.Split('`')[0])));

            return;
        }

        // EF Core, the logger and the base library are not ours, so the walk stops there
        if (!ours.Contains(type.Assembly) || path.Contains(type))
        {
            return;
        }

        var next = path.Append(type).ToList();

        if (!type.IsInterface && !type.IsAbstract)
        {
            FollowConstructors(type, next, ours, chains);

            return;
        }

        foreach (var implementation in ours.SelectMany(assembly => assembly.GetTypes())
                     .Where(candidate => candidate is { IsClass: true, IsAbstract: false } && candidate.IsAssignableTo(type)))
        {
            FollowConstructors(implementation, [.. next, implementation], ours, chains);
        }
    }

    private static void FollowConstructors(Type type, List<Type> path, Assembly[] ours, List<string> chains)
    {
        foreach (var parameter in type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
        {
            Follow(parameter.ParameterType, path, ours, chains);
        }
    }

    // the API and the Mosaic projects it references; the fakes in this test project are not walked
    private static Assembly[] OurAssemblies() =>
    [
        typeof(Program).Assembly,
        .. typeof(Program).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith(OwnPrefix, StringComparison.Ordinal))
            .Select(Assembly.Load),
    ];
}
