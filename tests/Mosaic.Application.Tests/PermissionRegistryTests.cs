using Mosaic.Application.Authorization;
using Mosaic.Contracts;
using Mosaic.Domain.Authorization;
using Mosaic.Domain.Errors;

namespace Mosaic.Application.Tests;

public sealed class PermissionRegistryTests
{
    private static PermissionRegistry WithCore() => new([new CorePermissionSource()]);

    private sealed class Source(params Permission[] permissions) : IPermissionSource
    {
        public IReadOnlyList<Permission> Permissions { get; } = permissions;
    }

    [Fact]
    public void Parse_ForARegisteredKey_ReturnsThePermission()
    {
        Assert.Equal(CorePermissions.Deploy, WithCore().Parse("core.apps.deploy"));
    }

    [Fact]
    public void Parse_ForAKeyNobodyRegistered_RefusesWithTheCode()
    {
        var refused = Assert.Throws<InvalidRequestException>(() => WithCore().Parse("core.apps.deply"));

        Assert.Equal(ErrorCodes.UnknownPermission, refused.Code);
    }

    // e.g. the pro assembly claiming a key that core already owns
    [Fact]
    public void Construct_WithAKeyAnotherEditionAlreadyClaimed_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new PermissionRegistry(
            [new CorePermissionSource(), new Source(new Permission("core.apps.deploy"))]));
    }

    [Fact]
    public void Construct_PutsASecondEditionOnTopOfTheFirst()
    {
        var registry = new PermissionRegistry(
            [new CorePermissionSource(), new Source(new Permission("pro.tenants.manage"))]);

        Assert.True(registry.IsRegistered("pro.tenants.manage"));
        Assert.Equal(CorePermissions.All.Count + 1, registry.All.Count);
    }

    [Fact]
    public void IsRegistered_ForAKeyNobodyRegistered_IsFalse()
    {
        Assert.False(WithCore().IsRegistered("pro.tenants.manage"));
    }
}
