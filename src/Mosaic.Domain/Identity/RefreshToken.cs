using Mosaic.Domain.Common;

namespace Mosaic.Domain.Identity;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    // A hash of the token, never the token itself. Issue expects the caller to have hashed it.
    public string Token { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    // The hash of the token issued in its place, so a rotation can be followed from row to row.
    public string? ReplacedByToken { get; private set; }

    public static RefreshToken Issue(Guid userId, string token, DateTimeOffset now, TimeSpan lifetime)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Token = token,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset at, string? replacedByToken = null)
    {
        RevokedAt = at;
        ReplacedByToken = replacedByToken;
    }
}
