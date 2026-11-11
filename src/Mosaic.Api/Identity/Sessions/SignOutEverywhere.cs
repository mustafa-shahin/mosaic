using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class SignOutEverywhere(ManagementDbContext database, TimeProvider clock)
{
    public async Task HandleAsync(Guid userId, AuditOrigin auditOrigin, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var user = await database.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} passed the session check but was not found.");

        var now = clock.GetUtcNow();

        // the session that asked ends too, because its row holds the old stamp like every other
        user.EndAllSessions();

        var auditActor = new AuditActor(user.Id, user.FullName, user.Email);
        var auditTarget = new AuditTarget(AuditTargets.User, user.Id, user.FullName);
        var auditRow = AuditEntry.Succeeded(AuditActions.SignOutEverywhere, auditActor, auditTarget, auditOrigin, now);

        await database.SaveChangesAsync(auditRow, cancellationToken);
    }
}
