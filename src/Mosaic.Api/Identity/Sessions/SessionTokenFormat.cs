using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Mosaic.Api.Identity.Sessions;

// a session token is mosaic_ and 32 random bytes in base64url; only the SHA-256 of the whole string is stored
public static class SessionTokenFormat
{
    // a secret scanner recognises a leaked token by it, and a value without it costs no database read
    private const string Prefix = "mosaic_";

    private const int RandomBytes = 32;

    // 32 bytes in base64url without padding
    private const int EncodedLength = 43;

    public static (string Token, byte[] Hash) Create()
    {
        var token = Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes));

        return (token, Hash(token));
    }

    // null for anything Create could not have made, so the caller refuses it before reading the database
    public static byte[]? HashOf(string? presented)
    {
        if (presented is null
            || presented.Length != Prefix.Length + EncodedLength
            || !presented.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        // IsValid, because TryDecodeFromChars throws on bad input; the length, because 43 spaces decode to 0 bytes
        if (!Base64Url.IsValid(presented.AsSpan(Prefix.Length), out var decodedLength) || decodedLength != RandomBytes)
        {
            return null;
        }

        return Hash(presented);
    }

    // the whole string with its prefix, so a later kind of token with another prefix can never hash the same
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
