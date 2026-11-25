using Mosaic.Api.Validation;

namespace Mosaic.Api.Auditing;

public static class AuditingServices
{
    public static IServiceCollection AddAuditing(this IServiceCollection services)
    {
        services.AddSingleton<PageRequestValidator>();
        services.AddSingleton<AuditFilterValidator>();
        services.AddScoped<ListAuditEntries>();
        services.AddScoped<ListAuditFilterChoices>();
        services.AddScoped<SearchAuditPeople>();

        return services;
    }
}
