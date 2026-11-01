using Microsoft.Extensions.Logging;
using Mosaic.Api.Identity.Registration;

namespace Mosaic.Api.Tests.Identity.Registration;

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

    // a restarted server keeps the token, which the operator may already have copied
    [Fact]
    public void ASecondInstance_ReadsTheTokenTheFirstWrote()
    {
        File().Verify(null);

        var token = System.IO.File.ReadAllText(_path);

        Assert.Equal(SetupTokenResult.Accepted, File().Verify(token));
    }

    // one request writes the token while the others look for it, repeated so that an overlap shows
    [Fact]
    public async Task RequestsArrivingTogether_AllGetAnAnswerAndLeaveOneWholeToken()
    {
        const int rounds = 50;
        const int requests = 8;

        for (var round = 0; round < rounds; round++)
        {
            System.IO.File.Delete(_path);

            var file = File();
            using var start = new Barrier(requests);

            var answers = Enumerable.Range(0, requests)
                .Select(_ => Task.Factory.StartNew(
                    () =>
                    {
                        start.SignalAndWait();

                        return file.Verify("guessed");
                    },
                    TestContext.Current.CancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default))
                .ToArray();

            Assert.All(await Task.WhenAll(answers), answer => Assert.Equal(SetupTokenResult.Invalid, answer));
            Assert.Equal(43, System.IO.File.ReadAllText(_path).Length);
        }

        // nothing the requests wrote on the way is left beside the token
        Assert.Single(Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_path)}*"));
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

    [Fact]
    public void Prepare_WritesTheTokenAndLogsWhereItIs()
    {
        File().Prepare();

        Assert.Equal(43, System.IO.File.ReadAllText(_path).Length);
        Assert.Contains(_path, Assert.Single(_logger.Messages), StringComparison.Ordinal);
    }

    // the operator may be copying it right now, so a second screen must not replace it
    [Fact]
    public void Prepare_KeepsATokenThatIsAlreadyThere()
    {
        var file = File();

        file.Prepare();

        var token = System.IO.File.ReadAllText(_path);

        file.Prepare();

        Assert.Equal(token, System.IO.File.ReadAllText(_path));
        Assert.Equal(SetupTokenResult.Accepted, file.Verify(token));
    }

    // a recreated container starts with an empty log, so every call names the file again
    [Fact]
    public void Prepare_LogsThePathEveryTime()
    {
        var file = File();

        file.Prepare();
        file.Prepare();

        Assert.Equal(2, _logger.Messages.Count);
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
