using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;

namespace Mosaic.Desktop.Services.Auditing;

public sealed class AuditApi(ApiClient api)
{
    private const string Prefix = ApiVersions.V1Path + "/" + AuditRoutes.Prefix + "/";

    public Task<ApiResponse<PageResult<AuditEntryResult>>> GetEntriesAsync(
        int page, AuditFilter filter, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder()
            .Add(Paging.PageParameter, page)
            .Add(AuditFilter.SucceededParameter, filter.Succeeded)
            .Add(AuditFilter.ActionParameter, filter.Action)
            .Add(AuditFilter.PersonParameter, filter.Person)
            .Add(AuditFilter.FromParameter, filter.From);

        return api.GetAsync<PageResult<AuditEntryResult>>(Prefix + AuditRoutes.Entries + query, cancellationToken);
    }

    public Task<ApiResponse<IReadOnlyList<AuditPersonResult>>> SearchPeopleAsync(string? search, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder().Add(AuditPeople.SearchParameter, search);

        return api.GetAsync<IReadOnlyList<AuditPersonResult>>(Prefix + AuditRoutes.People + query, cancellationToken);
    }

    public Task<ApiResponse<AuditFilterChoices>> GetFilterChoicesAsync(CancellationToken cancellationToken) =>
        api.GetAsync<AuditFilterChoices>(Prefix + AuditRoutes.Filters, cancellationToken);
}
