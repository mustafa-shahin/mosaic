using Mosaic.Domain.Authorization;

namespace Mosaic.Api.Authorization;

// No key for user administration (even listing users), editing roles or wiping the database, so no role can grant them.
public static class CorePermissions
{
    public static readonly Permission ViewProjects = new("core.projects.view");
    public static readonly Permission ManageProjects = new("core.projects.manage");

    public static readonly Permission ViewServers = new("core.servers.view");
    public static readonly Permission ManageServers = new("core.servers.manage");

    public static readonly Permission ViewApps = new("core.apps.view");
    public static readonly Permission ManageApps = new("core.apps.manage");

    // Provision creates an app on a server and Deploy only touches one that exists, so a role can grant deploying alone.
    public static readonly Permission Provision = new("core.apps.provision");
    public static readonly Permission Deploy = new("core.apps.deploy");
    public static readonly Permission PlaceDeploymentLock = new("core.apps.lock");

    public static readonly Permission ViewSecrets = new("core.secrets.view");

    public static readonly Permission ViewSystem = new("core.system.view");
    public static readonly Permission ManageSystem = new("core.system.manage");
    public static readonly Permission ManageIntegrations = new("core.integrations.manage");

    public static readonly Permission ViewAudit = new("core.audit.view");

    public static IReadOnlyList<Permission> All { get; } =
    [
        ViewProjects, ManageProjects,
        ViewServers, ManageServers,
        ViewApps, ManageApps, Provision, Deploy, PlaceDeploymentLock,
        ViewSecrets,
        ViewSystem, ManageSystem, ManageIntegrations,
        ViewAudit,
    ];
}