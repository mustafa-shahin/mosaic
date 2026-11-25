using Microsoft.EntityFrameworkCore;
using Mosaic.Contracts.Auditing;
using Mosaic.Domain.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class SignOutUser(ManagementDbContext database, TimeProvider clock)
{
    public async Task HandleAsync(Guid sessionId, AuditOrigin auditOrigin, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var session = await database.SessionTokens.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} passed the session check but was not found.");

        var user = await database.Users.FindAsync([session.UserId], cancellationToken)
            ?? throw new InvalidOperationException($"User {session.UserId} not found.");

        var auditActor = new AuditActor(user.Id, user.FullName, user.Email);
        var auditTarget = new AuditTarget(AuditTargets.User, user.Id, user.FullName);
        var auditRow = AuditEntry.Succeeded(AuditActions.SignOut, auditActor, auditTarget, auditOrigin, now);

        // the delete is sent at once and not by SaveChanges, so only this transaction keeps it and its row together
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // a statement that may delete nothing, so a second sign-out at the same moment does not fail
        await database.SessionTokens
            .Where(candidate => candidate.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        await database.SaveChangesAsync(auditRow, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
