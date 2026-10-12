using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Identity;

public sealed class CheckSession(IManagementDbContext database, TimeProvider clock, ILogger<CheckSession> logger)
{
    public async Task<SessionToken?> HandleAsync(string? presentedToken, CancellationToken cancellationToken)
    {
        // before the query, so a value sign-in could not have issued costs nothing
        if (SessionTokenFormat.HashOf(presentedToken) is not { } hash)
        {
            return null;
        }

        var match = await (
                from session in database.SessionTokens
                // a family has ended when its first row has, so every check reads that row too
                join first in database.SessionTokens on session.FamilyId equals first.Id
                // the query filter on Users leaves a deleted person out of the join, so their sessions stop at once
                join user in database.Users on session.UserId equals user.Id
                where session.TokenHash == hash
                select new { Session = session, FamilyEndsAt = first.ExpiresAt, User = user })
            .SingleOrDefaultAsync(cancellationToken);

        var now = clock.GetUtcNow();

        // one answer for every refusal, so the caller cannot tell which check failed
        if (match is null
            || match.FamilyEndsAt <= now
            || match.Session.SecurityStamp != match.User.SecurityStamp
            || !match.User.IsActive)
        {
            return null;
        }

        // after the checks above, so a session that has already ended for another reason raises no alarm
        if (match.Session.IsReplay(now))
        {
            await SessionFamily.EndForReuseAsync(database, logger, match.Session, now, cancellationToken);

            return null;
        }

        // a replayed row has ended by these rules too, so checking them first would hide every replay
        if (match.Session.HasEnded(now))
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
