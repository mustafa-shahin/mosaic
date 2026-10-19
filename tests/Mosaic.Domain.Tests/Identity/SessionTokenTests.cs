using System.Security.Cryptography;
using Mosaic.Domain.Identity;

namespace Mosaic.Domain.Tests.Identity;

public sealed class SessionTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Hash = SHA256.HashData("mosaic_token"u8);

    private static User NewOwner() => User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);

    private static SessionToken Issued() => SessionToken.Issue(NewOwner(), Hash, Now);

    [Fact]
    public void Issue_RecordsTheUserTheirStampAndTheHash()
    {
        var owner = NewOwner();

        var session = SessionToken.Issue(owner, Hash, Now);

        Assert.Equal(owner.Id, session.UserId);
        Assert.Equal(owner.SecurityStamp, session.SecurityStamp);
        Assert.Equal(Hash, session.TokenHash);
    }

    [Fact]
    public void Issue_EndsTwelveHoursAfterTheSignIn()
    {
        var session = Issued();

        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now.AddHours(12), session.ExpiresAt);
    }

    [Fact]
    public void HasEnded_OneTickBeforeTwelveHours_IsFalse()
    {
        Assert.False(Issued().HasEnded(Now.AddHours(12).AddTicks(-1)));
    }

    [Fact]
    public void HasEnded_TwelveHoursAfterTheSignIn_IsTrue()
    {
        Assert.True(Issued().HasEnded(Now.AddHours(12)));
    }
}
