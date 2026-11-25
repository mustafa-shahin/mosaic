using Microsoft.EntityFrameworkCore;
using Mosaic.Api.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Auditing;

public sealed class ListAuditEntries(
    ManagementDbContext database, PageRequestValidator validator, AuditFilterValidator filterValidator)
{
    public async Task<PageResult<AuditEntryResult>> HandleAsync(
        Guid callerId, PageRequest request, AuditFilter filter, CancellationToken cancellationToken)
    {
        validator.Validate(request).ThrowIfInvalid();
        filterValidator.Validate(filter).ThrowIfInvalid();

        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var caller = await database.Users.FindAsync([callerId], cancellationToken)
            ?? throw new InvalidOperationException($"User {callerId} passed the session check but was not found.");

        // nothing is sent to the database here: the count and the page below both start from this one description
        IQueryable<AuditEntry> entries = database.AuditEntries;

        if (filter.Succeeded is { } succeeded)
        {
            var outcome = succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed;

            entries = entries.Where(e => e.Outcome == outcome);
        }

        if (filter.Action is { } action)
        {
            entries = entries.Where(e => e.Action == action);
        }

        if (filter.Person is { } person)
        {
            entries = entries.Where(e => e.ActorId == person || e.TargetId == person);
        }

        if (filter.From is { } from)
        {
            // Npgsql writes a time into a timestamptz column only in UTC, and the address may carry another offset
            var since = from.ToUniversalTime();

            entries = entries.Where(e => e.OccurredAt >= since);
        }

        var totalItems = await entries.CountAsync(cancellationToken);

        // whether the log holds anything at all: the count answers it, unless a filter matched no row; then the log is asked once more
        var isEmptyWithoutFilters = totalItems == 0;

        if (isEmptyWithoutFilters && filter != new AuditFilter())
        {
            isEmptyWithoutFilters = !await database.AuditEntries.AnyAsync(cancellationToken);
        }

        var items = await entries
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(entry => new AuditEntryResult
            {
                Id = entry.Id,
                OccurredAt = entry.OccurredAt,
                Action = entry.Action,
                Succeeded = entry.Outcome == AuditOutcome.Succeeded,
                ErrorCode = entry.ErrorCode,
                ActorName = entry.ActorName,
                ActorEmail = entry.ActorEmail,
                TargetType = entry.TargetType,
                TargetName = entry.TargetName,
                CorrelationId = entry.CorrelationId,
                // the database answers null for anyone but the owner, so the address never leaves it for them
                IPAddress = caller.IsOwner ? EF.Functions.Host(entry.IPAddress!) : null,
            })
            .ToListAsync(cancellationToken);

        return new PageResult<AuditEntryResult>
        {
            Items = items,
            TotalItems = totalItems,
            ItemsPerPage = request.PageSize,
            CurrentPage = request.Page,
            IsEmptyWithoutFilters = isEmptyWithoutFilters,
        };
    }
}
