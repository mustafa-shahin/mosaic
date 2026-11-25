using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Mosaic.Api.Authorization;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;

namespace Mosaic.Api.Auditing;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditing(this IEndpointRouteBuilder endpoints)
    {
        var audit = endpoints.NewVersionedApi()
            .MapGroup(ApiVersions.RouteTemplate + "/" + AuditRoutes.Prefix)
            .HasApiVersion(ApiVersionParser.Default.Parse(ApiVersions.V1));

        audit.MapGet(AuditRoutes.Entries, Entries);
        audit.MapGet(AuditRoutes.Filters, Filters);
        audit.MapGet(AuditRoutes.People, People);

        return endpoints;
    }

    [AllowAnySignedIn]
    public static async Task<ApiResponse<PageResult<AuditEntryResult>>> Entries(
        ClaimsPrincipal user,
        [FromServices] ListAuditEntries listAuditEntries,
        CancellationToken cancellationToken,
        [FromQuery(Name = Paging.PageParameter)] int page = Paging.FirstPage,
        [FromQuery(Name = Paging.PageSizeParameter)] int pageSize = Paging.DefaultPageSize,
        [FromQuery(Name = AuditFilter.SucceededParameter)] bool? succeeded = null,
        [FromQuery(Name = AuditFilter.ActionParameter)] string? action = null,
        [FromQuery(Name = AuditFilter.PersonParameter)] Guid? person = null,
        [FromQuery(Name = AuditFilter.FromParameter)] DateTimeOffset? from = null)
    {
        var callerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var request = new PageRequest { Page = page, PageSize = pageSize };
        var filter = new AuditFilter { Succeeded = succeeded, Action = action, Person = person, From = from };

        return ApiResponse.Ok(await listAuditEntries.HandleAsync(callerId, request, filter, cancellationToken));
    }

    [AllowAnySignedIn]
    public static async Task<ApiResponse<AuditFilterChoices>> Filters(
        [FromServices] ListAuditFilterChoices listAuditFilterChoices,
        CancellationToken cancellationToken) =>
        ApiResponse.Ok(await listAuditFilterChoices.HandleAsync(cancellationToken));

    [AllowAnySignedIn]
    public static async Task<ApiResponse<IReadOnlyList<AuditPersonResult>>> People(
        [FromServices] SearchAuditPeople searchAuditPeople,
        CancellationToken cancellationToken,
        [FromQuery(Name = AuditPeople.SearchParameter)] string? search = null) =>
        ApiResponse.Ok(await searchAuditPeople.HandleAsync(search, cancellationToken));
}
