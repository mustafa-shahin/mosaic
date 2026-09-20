using System.Reflection;
using Mosaic.Application.Authorization;
using Mosaic.Domain.Authorization;

namespace Mosaic.Application.Tests.Authorization;

public sealed class CorePermissionsTests
{
    [Fact]
    public void EveryKeyIsUnique()
    {
        var keys = CorePermissions.All.Select(permission => permission.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryKeyStartsWithTheCorePrefix()
    {
        var wrong = CorePermissions.All
            .Where(permission => !permission.Key.StartsWith("core.", StringComparison.Ordinal))
            .Select(permission => permission.Key)
            .ToList();

        Assert.True(wrong.Count == 0, $"Not prefixed core.: {string.Join(", ", wrong)}");
    }

    // All is written out by hand, so a new field can be forgotten there.
    [Fact]
    public void AllHoldsEveryDeclaredPermission()
    {
        var declared = typeof(CorePermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Permission))
            .Select(field => (Permission)field.GetValue(null)!)
            .ToList();

        var missing = declared.Except(CorePermissions.All).Select(permission => permission.Key).Order().ToList();

        Assert.True(missing.Count == 0, $"Declared but not in All: {string.Join(", ", missing)}");
    }
}
