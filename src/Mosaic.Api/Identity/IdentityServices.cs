using Mosaic.Api.Identity.Registration;
using Mosaic.Api.Identity.Sessions;

namespace Mosaic.Api.Identity;

public static class IdentityServices
{
    // not AddIdentity, because ASP.NET Identity's AddIdentity<TUser, TRole> already has that name
    public static IServiceCollection AddAccounts(this IServiceCollection services, string setupTokenPath)
    {
        // one hasher for the whole API, because its gate lets only one hash run at a time
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<RegisterRequestValidator>();
        services.AddSingleton(provider =>
            new SetupTokenFile(setupTokenPath, provider.GetRequiredService<ILogger<SetupTokenFile>>()));
        services.AddScoped<GetRules>();
        services.AddScoped<RegisterUser>();
        services.AddScoped<SignInUser>();
        services.AddScoped<CheckSession>();
        services.AddScoped<GetSession>();
        services.AddScoped<SignOutUser>();
        services.AddScoped<SignOutEverywhere>();

        return services;
    }
}
