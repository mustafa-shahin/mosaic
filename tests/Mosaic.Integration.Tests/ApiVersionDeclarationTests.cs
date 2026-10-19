using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Mosaic.Integration.Tests;

public sealed class ApiVersionDeclarationTests
{
    // an undeclared controller silently takes the library's default version, so it moves when the default does
    [Fact]
    public void EveryController_DeclaresItsVersionOrThatItHasNone()
    {
        var undeclared = typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)))
            .Where(type => type.GetCustomAttribute<ApiVersionAttribute>() is null
                && type.GetCustomAttribute<ApiVersionNeutralAttribute>() is null)
            .Select(type => type.Name);

        Assert.Empty(undeclared);
    }
}
