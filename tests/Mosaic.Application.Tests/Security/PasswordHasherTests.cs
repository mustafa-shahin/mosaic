using System.Diagnostics;
using Mosaic.Application.Security;

namespace Mosaic.Application.Tests.Security;

public sealed class PasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    // the right length for 16 and 32 bytes, so each bad row below fails only on the part it changes
    private const string Salt = "AAAAAAAAAAAAAAAAAAAAAA";
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHash_VerifiesItsOwnPassword()
    {
        using var hasher = Hasher();

        var hash = await hasher.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.Success, await hasher.VerifyAsync(Password, hash, Token));
    }

    [Fact]
    public async Task AWrongPassword_IsRefused()
    {
        using var hasher = Hasher();

        var hash = await hasher.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync("correct horse battery stapl", hash, Token));
    }

    // random salt per hash, two users with the same password don't get the same row
    [Fact]
    public async Task TheSamePasswordTwice_ProducesDifferentHashes()
    {
        using var hasher = Hasher();

        Assert.NotEqual(
            await hasher.HashAsync(Password, Token),
            await hasher.HashAsync(Password, Token));
    }

    [Fact]
    public async Task AHash_CarriesTheParametersItWasMadeWith()
    {
        using var hasher = Hasher();

        var parts = (await hasher.HashAsync(Password, Token)).Split('$');

        Assert.Equal(6, parts.Length);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Equal("m=19456,t=2,p=1", parts[3]);
        Assert.DoesNotContain("=", parts[4], StringComparison.Ordinal);
        Assert.DoesNotContain("=", parts[5], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHashMadeAtALowerCost_VerifiesAndAsksForARehash()
    {
        using var cheap = Hasher();
        using var expensive = Hasher(memoryKib: 65536, iterations: 3);

        var hash = await cheap.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, await expensive.VerifyAsync(Password, hash, Token));
        Assert.Equal(PasswordVerificationResult.Success, await cheap.VerifyAsync(Password, hash, Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$" + Salt)]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$" + Salt + "$" + Hash)]
    [InlineData("$argon2id$v=16$m=19456,t=2,p=1$" + Salt + "$" + Hash)]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1,x=1$" + Salt + "$" + Hash)]
    [InlineData("$argon2id$v=19$m=019456,t=2,p=1$" + Salt + "$" + Hash)]
    [InlineData("$argon2id$v=19$m=19456,t=0,p=1$" + Salt + "$" + Hash)]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$" + Hash)]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$AAAAAAAAAAAAAAAAAAAAA*$" + Hash)]
    [InlineData("$argon2id$v=19$m=7,t=2,p=2$" + Salt + "$" + Hash)]
    public async Task AStoredValueThisBuildCannotRead_IsRefusedWithoutThrowing(string stored)
    {
        using var hasher = Hasher();

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, stored, Token));
    }

    [Fact]
    public async Task AHashWithATrailingNewline_IsRefused()
    {
        using var hasher = Hasher();

        var hash = await hasher.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, hash + "\n", Token));
    }

    // ü as one code point, and as u + combining diaeresis, in both orders
    [Theory]
    [InlineData("Gr\u00FC\u00DFe-Passwort1", "Gru\u0308\u00DFe-Passwort1")]
    [InlineData("Gru\u0308\u00DFe-Passwort1", "Gr\u00FC\u00DFe-Passwort1")]
    public async Task ThePasswordInTheOtherUnicodeForm_StillVerifies(string stored, string typed)
    {
        using var hasher = Hasher();

        var hash = await hasher.HashAsync(stored, Token);

        Assert.Equal(PasswordVerificationResult.Success, await hasher.VerifyAsync(typed, hash, Token));
    }

    // caps are 4x the shipped cost (256 MiB, 12 passes, 4 lanes), a row right at them still verifies
    [Theory]
    [InlineData(262_144, 1, 1)]
    [InlineData(19_456, 12, 1)]
    [InlineData(19_456, 1, 4)]
    public async Task ACostAtTheCaps_StillVerifies(int memoryKib, int iterations, int lanes)
    {
        using var writer = Hasher(memoryKib, iterations, lanes);
        using var hasher = Hasher();

        var hash = await writer.HashAsync(Password, Token);

        Assert.NotEqual(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, hash, Token));
    }

    // real hash and the right password, only the cap can refuse it
    [Theory]
    [InlineData(262_145, 1, 1)]
    [InlineData(19_456, 13, 1)]
    [InlineData(19_456, 1, 5)]
    public async Task ACostAboveTheCaps_IsRefusedEvenWithTheRightPassword(int memoryKib, int iterations, int lanes)
    {
        using var writer = Hasher(memoryKib, iterations, lanes);
        using var hasher = Hasher();

        var hash = await writer.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, hash, Token));
    }

    [Fact]
    public async Task AMissingAccount_IsRefused()
    {
        using var hasher = Hasher();

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, null, Token));
    }

    // the answer is Failed either way, only the elapsed time shows the hash ran
    [Fact]
    public async Task AMissingAccount_CostsAHash()
    {
        using var hasher = Hasher(memoryKib: 65536, iterations: 3);

        var started = Stopwatch.GetTimestamp();

        await hasher.VerifyAsync(Password, null, Token);

        Assert.True(Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task AnEmptyPassword_IsRefused()
    {
        using var hasher = Hasher();

        var hash = await hasher.HashAsync(Password, Token);

        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(string.Empty, hash, Token));
    }

    // hashing an empty password means the caller has a bug
    [Fact]
    public async Task HashingAnEmptyPassword_Throws()
    {
        using var hasher = Hasher();

        await Assert.ThrowsAsync<ArgumentException>(() => hasher.HashAsync(string.Empty, Token));
    }

    // cheap default cost so every test can hash quickly
    private static PasswordHasher Hasher(int memoryKib = 19456, int iterations = 2, int lanes = 1) => new(memoryKib, iterations, lanes);
}
