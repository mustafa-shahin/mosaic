using Mosaic.Application.Authorization;
using Mosaic.Contracts;
using Mosaic.Domain.Authorization;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests;

// A role row written by a pro build, read by a build that has never heard of pro's keys. That is what
// an expired licence or a core-only install looks like.
public sealed class PermissionBoundaryTests
{
    private const string ProKey = "pro.tenants.manage";

    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<Permission> ReadBack(params string[] stored)
        => Assert.IsAssignableFrom<IReadOnlyList<Permission>>(
            PermissionListConverter.Converter.ConvertFromProvider(stored));

    [Fact]
    public void AKeyThisBuildDoesNotDeclare_SurvivesTheRoundTrip()
    {
        var read = ReadBack("core.apps.deploy", ProKey);

        Assert.Equal(2, read.Count);
        Assert.Contains(new Permission(ProKey), read);

        var written = Assert.IsType<string[]>(PermissionListConverter.Converter.ConvertToProvider(read));

        Assert.Equal(["core.apps.deploy", ProKey], written);
    }

    [Fact]
    public void AKeyThisBuildDoesNotDeclare_GrantsNothingItCanCheck()
    {
        var user = User.Register("ada@example.com", "Ada", "Lovelace", "hash", null, Now);

        user.AddRole(Role.Create("Finance", null, ReadBack(ProKey), Now));

        var resolved = user.ResolvePermissions();

        Assert.False(resolved.Has(CorePermissions.ManageSystem));
        Assert.False(resolved.Has(CorePermissions.Deploy));
    }

    [Fact]
    public void TheRegistryStillRefusesAnUndeclaredKeyAsInput()
    {
        var refused = Assert.Throws<InvalidRequestException>(() => new PermissionRegistry([new CorePermissionSource()]).Parse(ProKey));

        Assert.Equal(ErrorCodes.UnknownPermission, refused.Code);
    }
}
