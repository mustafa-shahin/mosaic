using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Konscious.Security.Cryptography;

namespace Mosaic.Application.Security;

// stored as $argon2id$v=19$m=65536,t=3,p=1$<salt>$<hash>, each row carries the cost it was hashed with
public sealed partial class PasswordHasher : IDisposable
{
    // RFC 9106's second recommended set with 1 lane instead of 4, more lanes spread the same work over more cores
    // safe to raise, old rows still verify and come back as SuccessRehashNeeded
    private const int MemoryKib = 65536;
    private const int Iterations = 3;
    private const int Lanes = 1;

    // each hash holds 64 MiB and a whole core until it is done, so only one runs at a time
    private const int MaxConcurrent = 1;

    // headroom so a raised cost still verifies after a rollback, a row above it is treated as corrupt
    private const int Headroom = 4;
    private const int MaxMemoryKib = MemoryKib * Headroom;
    private const int MaxIterations = Iterations * Headroom;
    private const int MaxLanes = Lanes * Headroom;

    private const string Version = "19";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    // exactly what Encode writes: no '=' padding, 22 characters of salt (16 bytes), 43 of hash (32 bytes)
    // \z because $ also matches before a trailing newline
    private const string PhcPattern =
        @"^\$argon2id\$v=" + Version + @"\$m=([1-9][0-9]{0,6}),t=([1-9][0-9]{0,2}),p=([1-9][0-9]?)"
        + @"\$([A-Za-z0-9+/]{22})\$([A-Za-z0-9+/]{43})\z";

    private readonly Parameters _current;
    private readonly SemaphoreSlim _gate = new(MaxConcurrent, MaxConcurrent);

    public PasswordHasher()
        : this(MemoryKib, Iterations, Lanes)
    {
    }

    // tests pass a cheap cost, the real one is too slow to use in every test
    internal PasswordHasher(int memoryKib, int iterations, int lanes) => _current = new Parameters(memoryKib, iterations, lanes);

    public void Dispose() => _gate.Dispose();

    public async Task<string> HashAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);

        return Encode(_current, salt, await ComputeAsync(password, salt, _current, cancellationToken));
    }

    public async Task<PasswordVerificationResult> VerifyAsync(string password, string? storedHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            return PasswordVerificationResult.Failed;
        }

        // no account: hash anyway and throw the result away, so it takes as long as a real account at today's cost
        if (storedHash is null)
        {
            await ComputeAsync(password, RandomNumberGenerator.GetBytes(SaltBytes), _current, cancellationToken);

            return PasswordVerificationResult.Failed;
        }

        if (!TryDecode(storedHash, out var parameters, out var salt, out var expected))
        {
            return PasswordVerificationResult.Failed;
        }

        // with the cost stored in the row
        var computed = await ComputeAsync(password, salt, parameters, cancellationToken);

        if (!CryptographicOperations.FixedTimeEquals(expected, computed))
        {
            return PasswordVerificationResult.Failed;
        }

        return parameters.MemoryKib < _current.MemoryKib || parameters.Iterations < _current.Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    // WaitAsync hands the thread back to the pool while the request waits for the slot
    private async Task<byte[]> ComputeAsync(string password, byte[] salt, Parameters parameters, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            // ü can arrive as one character or as u + a combining mark, NFC turns both into the same bytes
            // needs ICU, in invariant globalization mode Normalize returns the string unchanged
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)))
            {
                Salt = salt,
                MemorySize = parameters.MemoryKib,
                Iterations = parameters.Iterations,
                DegreeOfParallelism = parameters.Lanes,
            };

            return await argon2.GetBytesAsync(HashBytes);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Encode(Parameters parameters, byte[] salt, byte[] hash) =>
        $"$argon2id$v={Version}$m={parameters.MemoryKib},t={parameters.Iterations},p={parameters.Lanes}"
        + $"${ToBase64(salt)}${ToBase64(hash)}";

    private static bool TryDecode(string encoded, out Parameters parameters, out byte[] salt, out byte[] hash)
    {
        parameters = default;
        salt = [];
        hash = [];

        var match = Phc().Match(encoded);

        if (!match.Success)
        {
            return false;
        }

        var memory = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var iterations = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var lanes = int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture);

        // RFC 9106 minimum is 8 blocks per lane, Konscious throws below 4
        if (memory < 8 * lanes || memory > MaxMemoryKib || iterations > MaxIterations || lanes > MaxLanes)
        {
            return false;
        }

        salt = FromBase64(match.Groups[4].Value);
        hash = FromBase64(match.Groups[5].Value);
        parameters = new Parameters(memory, iterations, lanes);

        return true;
    }

    [GeneratedRegex(PhcPattern)]
    private static partial Regex Phc();

    private static string ToBase64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');

    private static byte[] FromBase64(string value) =>
        Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));

    private readonly record struct Parameters(int MemoryKib, int Iterations, int Lanes);
}