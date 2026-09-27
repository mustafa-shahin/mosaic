using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Mosaic.Application.Security;

namespace Mosaic.Infrastructure.Security;

internal sealed partial class SetupTokenFile(string path, ILogger<SetupTokenFile> logger) : ISetupToken
{
    private const int TokenBytes = 32;

    public SetupTokenResult Verify(string? supplied)
    {
        var token = Ensure();

        if (string.IsNullOrEmpty(supplied))
        {
            // logged on every refusal, because a recreated container starts with an empty log
            LogTokenPath(logger, path);

            return SetupTokenResult.Missing;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(token))
            ? SetupTokenResult.Accepted
            : SetupTokenResult.Invalid;
    }

    // deleting a file that is not there is not an error
    public void Discard() => File.Delete(path);

    private string Ensure()
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        try
        {
            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

            // CreateNew, so two requests arriving together cannot both write a token
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Encoding.UTF8.GetBytes(token));
            }

            Restrict();

            return token;
        }
        // a read-only volume has no file to find, so that failure still throws
        catch (IOException) when (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }
    }

    // Windows has no unix file mode and the call throws there
    private void Restrict()
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The instance has no owner yet. The setup token is in {Path}")]
    private static partial void LogTokenPath(ILogger logger, string path);
}
