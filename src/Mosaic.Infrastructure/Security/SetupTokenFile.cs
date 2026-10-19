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
            // logged on every attempt without a token, because a recreated container starts with an empty log
            LogTokenPath(logger, path);

            return SetupTokenResult.Missing;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(token))
            ? SetupTokenResult.Accepted
            : SetupTokenResult.Invalid;
    }

    public void Prepare()
    {
        Ensure();

        LogTokenPath(logger, path);
    }

    // deleting a file that is not there is not an error
    public void Discard() => File.Delete(path);

    private string Ensure() => File.Exists(path) ? Read() : Create();

    private string Create()
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

        // written under another name first, so a request arriving meanwhile never opens a file still being written
        var draft = $"{path}.{Path.GetRandomFileName()}";

        try
        {
            using (var stream = new FileStream(draft, DraftOptions()))
            {
                stream.Write(Encoding.UTF8.GetBytes(token));
            }

            // refuses a file that is already there, so two requests arriving together cannot both place a token
            File.Move(draft, path, overwrite: false);

            return token;
        }
        // a read-only volume has no file to find, so that failure still throws
        catch (IOException) when (File.Exists(path))
        {
            return Read();
        }
        finally
        {
            File.Delete(draft);
        }
    }

    // on Windows the move that just placed the file may still hold it with delete access, so the read has to share that
    private string Read()
    {
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));

        return reader.ReadToEnd().Trim();
    }

    // only its owner can read it from the moment it exists; Windows has no unix file mode and the setter throws there
    private static FileStreamOptions DraftOptions()
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The instance has no owner yet. The setup token is in {Path}")]
    private static partial void LogTokenPath(ILogger logger, string path);
}
