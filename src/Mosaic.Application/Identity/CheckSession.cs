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

        var match = await (
                from session in database.SessionTokens
                // the query filter on Users leaves a deleted person out of the join, so their sessions stop at once
                join user in database.Users on session.UserId equals user.Id
                where session.TokenHash == hash
                select new { Session = session, User = user })
            .SingleOrDefaultAsync(cancellationToken);

        // one answer for every refusal, so the caller cannot tell which check failed
        if (match is null
            || match.Session.HasEnded(clock.GetUtcNow())
            || match.Session.SecurityStamp != match.User.SecurityStamp
            || !match.User.IsActive)
        {
            return null;
        }

        return match.Session;
    }
}
