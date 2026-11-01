using Mosaic.Contracts.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class GetSession(ManagementDbContext database)
{
    public async Task<SessionResult> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var user = await database.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} passed the session check but was not found.");

        return UserMapper.ToSessionResult(user);
    }
}
