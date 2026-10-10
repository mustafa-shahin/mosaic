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

    // a replaced token still serves the requests that were already on their way when it was replaced
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    private SessionToken()
    {
    }

    public Guid UserId { get; private set; }

    // the SHA-256 of the token, so a copy of the database signs nobody in
    public byte[] TokenHash { get; private set; } = null!;

    public Guid SecurityStamp { get; private set; }

    public DateTimeOffset LastUsedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    // every row one sign-in produced shares it: the first row's own id, copied by every rotation
    public Guid FamilyId { get; private set; }

    // the row this one replaced; unique in the database, so a token can be rotated only once
    public Guid? ReplacesId { get; private set; }

    public static SessionToken Issue(User user, byte[] tokenHash, DateTimeOffset now)
    {
        var id = Guid.CreateVersion7();

        return new()
        {
            Id = id,
            UserId = user.Id,
            TokenHash = tokenHash,
            SecurityStamp = user.SecurityStamp,
            FamilyId = id,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.Add(MaximumAge),
        };
    }

    // a sign-out sets ExpiresAt as well, so a revoked row that has not expired was replaced and keeps its grace
    public bool HasEnded(DateTimeOffset now) =>
        ExpiresAt <= now
        || LastUsedAt.Add(IdleWindow) <= now
        || (RevokedAt is { } replacedAt && replacedAt.Add(RotationGrace) <= now);

    // a replaced token passes the session check during its grace, but never rotates again
    public bool CanRotate(DateTimeOffset now) => RevokedAt is null && !HasEnded(now);

    // the new row keeps the family, the stamp and ExpiresAt, so rotating never stretches a session past its 30 days
    public SessionToken Rotate(byte[] tokenHash, DateTimeOffset now)
    {
        var next = new SessionToken
        {
            Id = Guid.CreateVersion7(),
            UserId = UserId,
            TokenHash = tokenHash,
            SecurityStamp = SecurityStamp,
            FamilyId = FamilyId,
            ReplacesId = Id,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = ExpiresAt,
        };

        RevokedAt = now;

        return next;
    }

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

    // ends this row alone, so the person's other sessions keep working
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
        ExpiresAt = now;
    }
}
