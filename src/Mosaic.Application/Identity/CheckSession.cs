using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Identity;

public sealed class CheckSession(IManagementDbContext database, TimeProvider clock)
{
    public async Task<SessionToken?> HandleAsync(string? presentedToken, CancellationToken cancellationToken)
    {
        // before the query, so a value sign-in could not have issued costs nothing
        if (SessionTokenFormat.HashOf(presentedToken) is not { } hash)
        {
            return null;
        }

        // the query filter on Users leaves a deleted person out of the join, so their sessions stop at once
        var match = await database.SessionTokens
            .Where(session => session.TokenHash == hash)
            .Join(database.Users, session => session.UserId, user => user.Id, (session, user) => new { Session = session, User = user })
            .SingleOrDefaultAsync(cancellationToken);

        var now = clock.GetUtcNow();

        // one answer for all four, so the caller cannot tell which one failed
        if (match is null
            || match.Session.HasEnded(now)
            || match.Session.SecurityStamp != match.User.SecurityStamp
            || !match.User.IsActive)
        {
            return null;
        }

        if (match.Session.Touch(now))
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        return match.Session;
    }
}
