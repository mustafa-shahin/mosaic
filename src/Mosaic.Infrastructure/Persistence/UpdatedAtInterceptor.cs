using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mosaic.Domain.Common;

namespace Mosaic.Infrastructure.Persistence;

// Sets UpdatedAt on every modified entity. Deletes pass through untouched: soft deletion happens in
// the domain, see SoftDeletableEntity.SoftDelete.
internal sealed class UpdatedAtInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<Timestamped>())
        {
            if (entry.State == EntityState.Modified)
            {
                // set through the change tracker so UpdatedAt can keep its private setter
                entry.Property(nameof(Timestamped.UpdatedAt)).CurrentValue = now;
            }
        }
    }
}
