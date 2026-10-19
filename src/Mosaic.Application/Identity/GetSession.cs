using Mosaic.Application.Persistence;
using Mosaic.Contracts.Identity;

namespace Mosaic.Application.Identity;

public sealed class GetSession(IManagementDbContext database)
{
    public async Task<SessionResult> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var user = await database.Users.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} passed the session check but was not found.");

        return UserMapper.ToSessionResult(user);
    }
}
