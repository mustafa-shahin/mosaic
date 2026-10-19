using System.Security.Cryptography;
using System.Text;
using Mosaic.Application.Security;

namespace Mosaic.Application.Tests.Security;

public sealed class SessionTokenFormatTests
{
    // 32 zero bytes in base64url, a shape Create can make
    private const string ZeroBytes = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public static TheoryData<string> NotMadeByCreate => new()
    {
        new string('A', 50),

        // the decoder skips whitespace, so these decode to 0 bytes
        "mosaic_" + new string(' ', 43),

        // a character of standard base64 that base64url does not use
        "mosaic_+" + ZeroBytes[1..],

        // the same 32 zero bytes with a padding bit set, a second spelling of one token
        "mosaic_" + ZeroBytes[..^1] + "B",

        "mosaic_" + ZeroBytes + "=",
    };

    [Fact]
    public void Create_StartsWithThePrefixAndHasTheExactLength()
    {
        var (token, _) = SessionTokenFormat.Create();

        Assert.StartsWith("mosaic_", token, StringComparison.Ordinal);
        Assert.Equal(50, token.Length);
    }

    [Fact]
    public void Create_GivesEveryCallItsOwnToken()
    {
        Assert.NotEqual(SessionTokenFormat.Create().Token, SessionTokenFormat.Create().Token);
    }

    [Fact]
    public void Create_HashesTheWholeToken()
    {
        var (token, hash) = SessionTokenFormat.Create();

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), hash);
    }

    [Fact]
    public void HashOf_ATokenCreateMade_IsTheHashCreateReturned()
    {
        var (token, hash) = SessionTokenFormat.Create();

        Assert.Equal(hash, SessionTokenFormat.HashOf(token));
    }

    [Fact]
    public void HashOf_AnyShapeCreateCanMake_IsAccepted()
    {
        Assert.NotNull(SessionTokenFormat.HashOf("mosaic_" + ZeroBytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a token")]
    [InlineData("mosaic_short")]
    [MemberData(nameof(NotMadeByCreate))]
    public void HashOf_AnythingCreateCouldNotMake_IsNull(string? presented)
    {
        Assert.Null(SessionTokenFormat.HashOf(presented));
    }
}
