using Mosaic.Application.Persistence;

namespace Mosaic.Application.Identity;

public sealed class SignOutUser(IManagementDbContext database, TimeProvider clock)
{
    public async Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var session = await database.SessionTokens.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} passed the session check but was not found.");

        session.Revoke(clock.GetUtcNow());

        await database.SaveChangesAsync(cancellationToken);
    }
}
