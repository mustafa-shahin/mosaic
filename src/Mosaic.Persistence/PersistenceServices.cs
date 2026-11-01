using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mosaic.Persistence;

public static class PersistenceServices
{
    // takes the value itself, so reading configuration stays in Program
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        // the interceptor stamps rows with it; TryAdd keeps a clock the host registered first
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<UpdatedAtInterceptor>();

        services.AddDbContext<ManagementDbContext>((provider, options) =>
        {
            DatabaseOptions.Configure(options, connectionString);
            options.AddInterceptors(provider.GetRequiredService<UpdatedAtInterceptor>());
        });

        return services;
    }
}
