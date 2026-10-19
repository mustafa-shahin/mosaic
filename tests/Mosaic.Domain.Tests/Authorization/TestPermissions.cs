using Mosaic.Domain.Authorization;

namespace Mosaic.Domain.Tests.Authorization;

// The domain does not care which keys exist, so its tests never use the real catalogue.
internal static class TestPermissions
{
    public static readonly Permission Deploy = new("test.deploy");
    public static readonly Permission ViewAudit = new("test.audit.view");
    public static readonly Permission ManageSystem = new("test.system.manage");
    public static readonly Permission ViewProjects = new("test.projects.view");
}
