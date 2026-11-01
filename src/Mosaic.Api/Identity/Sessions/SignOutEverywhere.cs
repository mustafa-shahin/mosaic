using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class SignOutEverywhere(ManagementDbContext database)
{
    public async Task HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var user = await database.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} passed the session check but was not found.");

        // the session that asked ends too, because its row holds the old stamp like every other
        user.EndAllSessions();

        await database.SaveChangesAsync(cancellationToken);
    }
}
