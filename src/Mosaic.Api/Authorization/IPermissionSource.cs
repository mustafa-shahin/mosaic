using Mosaic.Domain.Authorization;

namespace Mosaic.Api.Authorization;

// One per edition. Pro registers its own, so core never names pro.
public interface IPermissionSource
{
    IReadOnlyList<Permission> Permissions { get; }
}

public sealed class CorePermissionSource : IPermissionSource
{
    public IReadOnlyList<Permission> Permissions => CorePermissions.All;
}
