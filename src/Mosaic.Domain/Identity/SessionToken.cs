using Mosaic.Domain.Common;

namespace Mosaic.Domain.Identity;

// one row is one session; the token itself lives only on the desktop
public sealed class SessionToken : Entity
{
    // a token left on a machine nobody opens any more stops working after this
    private static readonly TimeSpan IdleWindow = TimeSpan.FromDays(7);

    // however often it is used, a session ends this long after the sign-in that started it
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(30);

    // LastUsedAt moves at most this often, otherwise every request would also be a write
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    private SessionToken()
    {
    }

    public Guid UserId { get; private set; }

    // the SHA-256 of the token, so a copy of the database signs nobody in
    public byte[] TokenHash { get; private set; } = null!;

    public Guid SecurityStamp { get; private set; }

    public DateTimeOffset LastUsedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static SessionToken Issue(User user, byte[] tokenHash, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = tokenHash,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.Add(MaximumAge),
        };

    public bool HasEnded(DateTimeOffset now) => ExpiresAt <= now || LastUsedAt.Add(IdleWindow) <= now;

    // true only when LastUsedAt moved, so the caller saves only then
    public bool Touch(DateTimeOffset now)
    {
        if (LastUsedAt.Add(TouchInterval) > now)
        {
            return false;
        }

        LastUsedAt = now;

        return true;
    }
}
