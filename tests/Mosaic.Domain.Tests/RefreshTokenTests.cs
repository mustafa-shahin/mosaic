using Mosaic.Domain.Identity;

namespace Mosaic.Domain.Tests;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static RefreshToken Issued() => RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now, Lifetime);

    [Fact]
    public void Issue_ExpiresAfterTheLifetime()
    {
        Assert.Equal(Now.Add(Lifetime), Issued().ExpiresAt);
    }

    [Fact]
    public void IsActive_BeforeItExpires_IsTrue()
    {
        Assert.True(Issued().IsActive(Now.AddDays(6)));
    }

    [Fact]
    public void IsActive_OnceItHasExpired_IsFalse()
    {
        Assert.False(Issued().IsActive(Now.AddDays(8)));
    }

    [Fact]
    public void Revoke_EndsItEarlyAndRecordsTheSuccessor()
    {
        var token = Issued();

        token.Revoke(Now.AddDays(1), "next-hash");

        Assert.False(token.IsActive(Now.AddDays(2)));
        Assert.Equal("next-hash", token.ReplacedByToken);
    }
}
