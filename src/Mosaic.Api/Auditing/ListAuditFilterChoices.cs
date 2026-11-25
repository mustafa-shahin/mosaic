using Microsoft.EntityFrameworkCore;
using Mosaic.Contracts.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Auditing;

public sealed class ListAuditFilterChoices(ManagementDbContext database)
{
    public async Task<AuditFilterChoices> HandleAsync(CancellationToken cancellationToken)
    {
        var actions = await database.AuditEntries
            .Select(entry => entry.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken);

        return new AuditFilterChoices { Actions = actions };
    }
}
