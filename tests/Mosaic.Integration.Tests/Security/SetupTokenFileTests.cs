using Microsoft.Extensions.Logging;
using Mosaic.Application.Security;
using Mosaic.Infrastructure.Security;

namespace Mosaic.Integration.Tests.Security;

public sealed class SetupTokenFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"mosaic-setup-token-{Guid.CreateVersion7():N}");
    private readonly RecordingLogger _logger = new();

    [Fact]
    public void TheFirstCheck_WritesThirtyTwoRandomBytesAsBase64Url()
    {
        var result = File().Verify(null);

        var token = System.IO.File.ReadAllText(_path);

        Assert.Equal(SetupTokenResult.Missing, result);
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    // the second one finds the file already there, which is the branch two simultaneous requests take
    [Fact]
    public void ASecondInstance_ReadsTheTokenTheFirstWrote()
    {
        File().Verify(null);

        var token = System.IO.File.ReadAllText(_path);

        Assert.Equal(SetupTokenResult.Accepted, File().Verify(token));
    }

    [Fact]
    public void AWrongToken_IsInvalid()
    {
        Assert.Equal(SetupTokenResult.Invalid, File().Verify("guessed"));
    }

    [Fact]
    public void AMissingToken_LogsWhereTheFileIs()
    {
        File().Verify(null);

        Assert.Contains(_path, Assert.Single(_logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public void ASuppliedToken_LogsNothing()
    {
        File().Verify("guessed");

        Assert.Empty(_logger.Messages);
    }

    [Fact]
    public void Discard_RemovesTheFileAndToleratesNone()
    {
        var file = File();

        file.Verify(null);
        file.Discard();
        file.Discard();

        Assert.False(System.IO.File.Exists(_path));
    }

    public void Dispose() => System.IO.File.Delete(_path);

    private SetupTokenFile File() => new(_path, _logger);

    private sealed class RecordingLogger : ILogger<SetupTokenFile>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
