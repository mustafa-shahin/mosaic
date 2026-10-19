using Mosaic.Domain.Common;

namespace Mosaic.Domain.Identity;

// one row per sign-in; the token itself goes only to the client that signed in
public sealed class SessionToken : Entity
{
    // however often it is used, a session ends this long after the sign-in that started it
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(12);

    private SessionToken()
    {
    }

    public Guid UserId { get; private set; }

    // the SHA-256 of the token, so a copy of the database signs nobody in
    public byte[] TokenHash { get; private set; } = null!;

    public Guid SecurityStamp { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static SessionToken Issue(User user, byte[] tokenHash, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = user.Id,
        TokenHash = tokenHash,
        SecurityStamp = user.SecurityStamp,
        CreatedAt = now,
        ExpiresAt = now.Add(MaximumAge),
    };

    public bool HasEnded(DateTimeOffset now) => ExpiresAt <= now;
}
