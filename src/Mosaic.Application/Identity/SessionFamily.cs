using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mosaic.Application.Persistence;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Identity;

// every session check reads the family's first row, so writing that one row ends every token the sign-in produced
internal static partial class SessionFamily
{
    // true only when this call ended the family, so a family that had already ended is not reported twice
    public static async Task<bool> EndAsync(IManagementDbContext database, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // a statement of its own, which UpdatedAtInterceptor never sees, so it sets updated_at itself
        var ended = await database.SessionTokens
            .Where(first => first.Id == familyId && first.ExpiresAt > now)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(first => first.ExpiresAt, now)
                    .SetProperty(first => first.UpdatedAt, now),
                cancellationToken);

        return ended == 1;
    }

    public static async Task EndForReuseAsync(IManagementDbContext database, ILogger logger, SessionToken replayed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await EndAsync(database, replayed.FamilyId, now, cancellationToken))
        {
            LogTokenReuse(logger, replayed.UserId, replayed.FamilyId, replayed.Id);
        }
    }

    // no token, no hash and no address, because Mosaic sets no limit on how long its log is kept
    [LoggerMessage(Level = LogLevel.Warning, Message = "A replaced session token was presented again, so session family {FamilyId} of user {UserId} was ended (presented row {SessionId})")]
    private static partial void LogTokenReuse(ILogger logger, Guid userId, Guid familyId, Guid sessionId);
}
