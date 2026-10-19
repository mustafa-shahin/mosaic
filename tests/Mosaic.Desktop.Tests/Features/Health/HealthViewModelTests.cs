using System.Net;
using Mosaic.Contracts;
using Mosaic.Desktop.Controls;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Health;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Features.Health;

public sealed class HealthViewModelTests
{
    private static string HealthyBody(string apiContract) =>
        $$"""{"data":{"service":"Mosaic.Api","serverTimeUtc":"2026-01-01T09:30:00+00:00","apiContract":"{{apiContract}}"},"success":true}""";

    private const string UnversionedBody =
        """{"data":{"service":"Mosaic.Api","serverTimeUtc":"2026-01-01T09:30:00+00:00"},"success":true}""";

    private static Localizer English() => Localizer.Load(LanguageCatalog.Resolve("en"));

    private static HealthViewModel ViewModelWith(HttpMessageHandler handler) =>
        new(new HealthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, new CurrentSession())), English());

    [Fact]
    public async Task Check_WhenTheApiAnswers_ReportsReachableAndKeepsTheServiceName()
    {
        var viewModel = ViewModelWith(
            new CannedResponseHandler(HttpStatusCode.OK, HealthyBody(ApiContract.Version)));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Reachable, viewModel.State);
        Assert.True(viewModel.IsReachable);
        Assert.False(viewModel.IsChecking);
        Assert.Equal("Mosaic.Api", viewModel.Service);
        Assert.Null(viewModel.ErrorCode);
    }

    [Fact]
    public async Task Check_WhenTheApiCannotBeReached_ReportsUnreachableWithAClientErrorCode()
    {
        var viewModel = ViewModelWith(new ThrowingHandler(new HttpRequestException("no route to host")));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Unreachable, viewModel.State);
        Assert.Equal(DesktopErrorCodes.ApiUnreachable, viewModel.ErrorCode);
        Assert.Null(viewModel.Service);
    }

    [Fact]
    public async Task Check_WhenTheServerReturnsSomethingThatIsNotTheEnvelope_ReportsUnexpectedResponse()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.NotFound, "<html>nope</html>"));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Unreachable, viewModel.State);
        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, viewModel.ErrorCode);
    }

    [Fact]
    public async Task Check_WhenTheServerSpeaksAnotherMajor_ReportsIncompatibleRatherThanUnreachable()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, HealthyBody("2.0")));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Incompatible, viewModel.State);
        Assert.False(viewModel.IsUnreachable);
        Assert.Equal("2.0", viewModel.ServerContract);
        Assert.Contains("2.0", viewModel.ContractSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_WhenTheServerPredatesTheVersionField_ReportsIncompatible()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, UnversionedBody));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Incompatible, viewModel.State);
        Assert.Null(viewModel.ServerContract);
    }

    [Fact]
    public async Task Check_WhenTheServerIsAheadByAMinor_IsStillReachable()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, HealthyBody("1.99")));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Reachable, viewModel.State);
    }

    // ApiClient throws on a deliberate cancellation; the screen is where that stops, because an
    // exception escaping the command would reach the dispatcher and kill the process.
    [Fact]
    public async Task Check_WhenTheCallerCancels_StopsQuietlyInsteadOfThrowing()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());
        using var cancellation = new CancellationTokenSource();

        var check = viewModel.CheckAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await check;

        Assert.Equal(ConnectionState.Checking, viewModel.State);
        Assert.Null(viewModel.ErrorCode);
    }

    // What the shell does to the screen it is leaving.
    [Fact]
    public async Task Dispose_WhileACheckIsRunning_EndsItWithoutFaulting()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());

        viewModel.CheckIfIdle();

        var running = viewModel.CheckCommand.ExecutionTask!;

        viewModel.Dispose();

        await running;

        Assert.True(running.IsCompletedSuccessfully);
        Assert.False(viewModel.CheckCommand.IsRunning);
    }

    [Fact]
    public async Task Check_WhenNoApiAddressIsConfigured_ReportsUnreachableWithoutCallingTheNetwork()
    {
        var viewModel = new HealthViewModel(
            new HealthApi(new ApiClient(new HttpClient(new ThrowingHandler(new InvalidOperationException("must not be called"))), new CurrentSession())),
            English());

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Unreachable, viewModel.State);
        Assert.Equal(DesktopErrorCodes.ApiAddressInvalid, viewModel.ErrorCode);
    }

    [Fact]
    public async Task Check_WhenTheApiCannotBeReached_ShowsABadStatusAndTheTranslatedReason()
    {
        var viewModel = ViewModelWith(new ThrowingHandler(new HttpRequestException("no route to host")));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusLevel.Bad, viewModel.Status);
        Assert.Equal(viewModel.Strings.Translate("health.unreachable"), viewModel.StatusText);
        Assert.Equal(viewModel.Strings.Translate(DesktopErrorCodes.ApiUnreachable), viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Check_WhenTheServerRefuses_KeepsTheValuesItSentWithTheCode()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(
            HttpStatusCode.NotFound,
            """{"error":{"code":"deployment.not_found","params":{"name":"tessera"}}}"""));

        await viewModel.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal("deployment.not_found", viewModel.ErrorCode);
        Assert.Equal("tessera", viewModel.ErrorParams!["name"]);
    }

    // HealthView calls this each time its DataContext becomes a HealthViewModel.
    [Fact]
    public void CheckIfIdle_WhileACheckIsRunning_LeavesTheFirstOneAlone()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());

        viewModel.CheckIfIdle();

        var first = viewModel.CheckCommand.ExecutionTask;

        viewModel.CheckIfIdle();

        Assert.Same(first, viewModel.CheckCommand.ExecutionTask);
        Assert.False(first!.IsCanceled);
        Assert.False(first.IsFaulted);
        Assert.True(viewModel.CheckCommand.IsRunning);
    }
}
