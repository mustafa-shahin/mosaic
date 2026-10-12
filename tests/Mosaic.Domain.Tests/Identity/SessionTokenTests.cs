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
    public void Issue_StartsAFamilyOfItsOwn()
    {
        var session = Issued();

        Assert.Equal(session.Id, session.FamilyId);
        Assert.Null(session.ReplacesId);
    }

    [Fact]
    public void Rotate_KeepsThePersonTheStampTheFamilyAndTheEnd()
    {
        var session = Issued();
        var newHash = SHA256.HashData("mosaic_next"u8);

        var next = session.Rotate(newHash, Now.AddHours(1));

        Assert.NotEqual(session.Id, next.Id);
        Assert.Equal(session.UserId, next.UserId);
        Assert.Equal(session.SecurityStamp, next.SecurityStamp);
        Assert.Equal(session.FamilyId, next.FamilyId);
        Assert.Equal(session.ExpiresAt, next.ExpiresAt);
        Assert.Equal(session.Id, next.ReplacesId);
        Assert.Equal(newHash, next.TokenHash);
        Assert.Equal(Now.AddHours(1), next.CreatedAt);
        Assert.Equal(Now.AddHours(1), next.LastUsedAt);
    }

    [Fact]
    public void Rotate_MarksTheOldRowReplaced()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), session.RevokedAt);
    }

    // a request that was on its way when the new token was issued still finishes
    [Fact]
    public void HasEnded_ForAReplacedRowInsideItsGrace_IsFalse()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.False(session.HasEnded(Now.AddHours(1).AddSeconds(29)));
    }

    [Fact]
    public void HasEnded_ForAReplacedRowAfterItsGrace_IsTrue()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.True(session.HasEnded(Now.AddHours(1).AddSeconds(30)));
    }

    [Fact]
    public void CanRotate_ALiveRow_IsTrue()
    {
        Assert.True(Issued().CanRotate(Now.AddHours(1)));
    }

    [Fact]
    public void CanRotate_AReplacedRowInsideItsGrace_IsFalse()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.False(session.CanRotate(Now.AddHours(1).AddSeconds(10)));
    }

    [Fact]
    public void IsReplay_ForALiveRow_IsFalse()
    {
        Assert.False(Issued().IsReplay(Now.AddHours(1)));
    }

    // a request that left with the old token just before the rotation is no alarm
    [Fact]
    public void IsReplay_ForAReplacedRowInsideItsGrace_IsFalse()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.False(session.IsReplay(Now.AddHours(1).AddSeconds(29)));
    }

    [Fact]
    public void IsReplay_ForAReplacedRowAfterItsGrace_IsTrue()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.True(session.IsReplay(Now.AddHours(1).AddSeconds(30)));
    }

    // nobody uses a replaced row again, so its idle window has passed by the time a thief tries it
    [Fact]
    public void IsReplay_ForAReplacedRowAWeekLater_IsTrue()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.True(session.IsReplay(Now.AddDays(8)));
    }

    [Fact]
    public void IsReplay_ForAReplacedRowPastItsOwnEnd_IsFalse()
    {
        var session = Issued();

        session.Rotate(Hash, Now.AddHours(1));

        Assert.False(session.IsReplay(Now.AddDays(30)));
    }

    // only a new sign-in starts a new 30 days, however often the desktop rotates
    [Fact]
    public void ATokenRotatedEveryHour_StillEndsThirtyDaysAfterTheSignIn()
    {
        var current = Issued();

        for (var hour = 1; hour < 30 * 24; hour++)
        {
            current = current.Rotate(Hash, Now.AddHours(hour));
        }

        Assert.False(current.HasEnded(Now.AddDays(30).AddMinutes(-1)));
        Assert.True(current.HasEnded(Now.AddDays(30)));
    }
}
