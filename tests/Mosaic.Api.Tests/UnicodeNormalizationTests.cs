using System.Diagnostics;

namespace Mosaic.Api.Tests;

public sealed class UnicodeNormalizationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the runtime reads invariant mode once at process start, so only a fresh process can turn it on
    [Fact]
    public async Task InInvariantGlobalizationMode_TheApiRefusesToStart()
    {
        var start = new ProcessStartInfo("dotnet", $"\"{typeof(Program).Assembly.Location}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "true";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        using var api = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);

        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        var output = api.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = api.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await api.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            // without the check the API starts and would run until killed
            if (!api.HasExited)
            {
                api.Kill(entireProcessTree: true);
            }
        }

        Assert.NotEqual(0, api.ExitCode);
        Assert.Contains("invariant globalization mode", await output + await error, StringComparison.Ordinal);
    }
}
