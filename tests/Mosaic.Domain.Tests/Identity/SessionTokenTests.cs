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
    public void Issue_CountsTheSignInAsTheFirstUse()
    {
        var session = Issued();

        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now, session.LastUsedAt);
    }

    [Fact]
    public void Issue_EndsThirtyDaysAfterTheSignIn()
    {
        Assert.Equal(Now.AddDays(30), Issued().ExpiresAt);
    }

    [Fact]
    public void HasEnded_WhileTheSessionIsUsed_IsFalse()
    {
        var session = Issued();

        session.Touch(Now.AddDays(6));

        Assert.False(session.HasEnded(Now.AddDays(10)));
    }

    // the last use was a day ago, so only the maximum age can end it here
    [Fact]
    public void HasEnded_ThirtyDaysAfterTheSignIn_IsTrue()
    {
        var session = Issued();

        session.Touch(Now.AddDays(29));

        Assert.True(session.HasEnded(Now.AddDays(30)));
    }

    [Fact]
    public void HasEnded_SevenDaysWithoutUse_IsTrue()
    {
        Assert.True(Issued().HasEnded(Now.AddDays(7)));
    }

    [Fact]
    public void Touch_WithinFiveMinutes_ChangesNothing()
    {
        var session = Issued();

        Assert.False(session.Touch(Now.AddMinutes(4)));
        Assert.Equal(Now, session.LastUsedAt);
    }

    [Fact]
    public void Touch_AfterFiveMinutes_MovesLastUsedAt()
    {
        var session = Issued();

        Assert.True(session.Touch(Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(5), session.LastUsedAt);
    }

    [Fact]
    public void Revoke_EndsTheSessionAtOnce()
    {
        var session = Issued();

        session.Revoke(Now.AddMinutes(1));

        Assert.True(session.HasEnded(Now.AddMinutes(1)));
    }

    [Fact]
    public void Revoke_RecordsWhen()
    {
        var session = Issued();

        session.Revoke(Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
    }
}
