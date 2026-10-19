using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;

namespace Mosaic.Application.Identity;

public sealed class SignOutUser(IManagementDbContext database)
{
    public async Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // a statement that may delete nothing, so a second sign-out at the same moment does not fail
        await database.SessionTokens
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
