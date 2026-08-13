using System.Net;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop.Tests;

public sealed class HealthViewModelTests
{
    private const string HealthyBody =
        """{"data":{"service":"Mosaic.Api","serverTimeUtc":"2026-01-01T09:30:00+00:00"},"success":true}""";

    private static HealthViewModel ViewModelWith(HttpMessageHandler handler) =>
        new(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public async Task Check_WhenTheApiAnswers_ReportsReachableAndKeepsTheServiceName()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, HealthyBody));

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
    public async Task Check_WhenTheCallerCancels_ThrowsRatherThanReportingUnreachable()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());
        using var cancellation = new CancellationTokenSource();

        var check = viewModel.CheckAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
    }
}
