using Mosaic.Domain.Authorization;
using Mosaic.Domain.Identity;

namespace Mosaic.Domain.Tests;

public sealed class RoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsTheName()
    {
        Assert.Equal("DevOps", Role.Create("  DevOps  ", null, [], Now).Name);
    }

    [Fact]
    public void Create_KeepsOneCopyOfARepeatedPermission()
    {
        var role = Role.Create("Dev", null, [TestPermissions.Deploy, TestPermissions.Deploy], Now);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void SetPermissions_ReplacesRatherThanAdds()
    {
        var role = Role.Create("Dev", null, [TestPermissions.Deploy], Now);

        role.SetPermissions([TestPermissions.ViewAudit]);

        Assert.Single(role.Permissions);
        Assert.Equal(TestPermissions.ViewAudit, role.Permissions[0]);
    }
}
