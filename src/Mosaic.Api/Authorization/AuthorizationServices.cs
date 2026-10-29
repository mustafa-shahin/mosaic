namespace Mosaic.Api.Authorization;

public static class AuthorizationServices
{
    public static IServiceCollection AddPermissions(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionSource, CorePermissionSource>();
        services.AddSingleton<PermissionRegistry>();

        return services;
    }
}
