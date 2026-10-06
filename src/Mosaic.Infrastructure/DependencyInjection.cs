using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Mosaic.Application.Authorization;
using Mosaic.Application.Identity;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Infrastructure.Persistence;
using Mosaic.Infrastructure.Security;

namespace Mosaic.Infrastructure;

public static class DependencyInjection
{
    // Takes the values themselves so that reading configuration stays in Program.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string setupTokenPath)
    {
        services.TryAddSingleton(TimeProvider.System);
        // AddMosaicPro appends its own source after this call.
        services.AddSingleton<IPermissionSource, CorePermissionSource>();
        services.AddSingleton<PermissionRegistry>();
        services.AddSingleton<UpdatedAtInterceptor>();

        services.AddDbContext<ManagementDbContext>((provider, options) =>
        {
            DatabaseOptions.Configure(options, connectionString);
            options.AddInterceptors(provider.GetRequiredService<UpdatedAtInterceptor>());
        });

        services.AddScoped<IManagementDbContext>(provider => provider.GetRequiredService<ManagementDbContext>());

        // one hasher for the whole API, because its gate lets only one hash run at a time
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<RegisterRequestValidator>();
        services.AddSingleton<ISetupToken>(provider =>
            new SetupTokenFile(setupTokenPath, provider.GetRequiredService<ILogger<SetupTokenFile>>()));
        services.AddScoped<RegisterUser>();
        services.AddScoped<SignInUser>();
        services.AddScoped<CheckSession>();
        services.AddScoped<GetSession>();
        services.AddScoped<SignOutUser>();
        services.AddScoped<SignOutEverywhere>();

        return services;
    }
}
