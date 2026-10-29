using Microsoft.EntityFrameworkCore;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class SignOutUser(ManagementDbContext database)
{
    public async Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // a statement that may delete nothing, so a second sign-out at the same moment does not fail
        await database.SessionTokens
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
