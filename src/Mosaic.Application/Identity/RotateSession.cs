using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;

namespace Mosaic.Application.Identity;

public sealed class RotateSession(IManagementDbContext database, TimeProvider clock)
{
    // PostgreSQL's code for a unique violation; DbException carries it, so no Npgsql type is needed here
    private const string UniqueViolation = "23505";

    public async Task<RotateResult> HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // CheckSession loaded this row earlier in the same request, so Find returns it without a query
        var session = await database.SessionTokens.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} passed the session check but was not found.");

        var now = clock.GetUtcNow();

        if (!session.CanRotate(now))
        {
            throw new NotPermittedException(ErrorCodes.SessionReplayed);
        }

        var (token, hash) = SessionTokenFormat.Create();

        database.SessionTokens.Add(session.Rotate(hash, now));

        // another rotation of the same token may have read the row first and saved first
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is DbException { SqlState: UniqueViolation })
        {
            throw new NotPermittedException(ErrorCodes.SessionReplayed);
        }

        return new RotateResult { Token = token };
    }
}
