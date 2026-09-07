using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mosaic.Application.Authorization;
using Mosaic.Application.Persistence;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Infrastructure;

public static class DependencyInjection
{
    // Takes the connection string itself so that reading configuration stays in Program.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
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

        return services;
    }
}
