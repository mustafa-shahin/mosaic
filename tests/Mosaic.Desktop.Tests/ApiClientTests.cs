using System.Net;
using Mosaic.Contracts;
using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Tests;

public sealed class ApiClientTests
{
    private static ApiClient ClientWith(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    private static Task<ApiResponse<HealthResult>> Get(HttpMessageHandler handler) =>
        ClientWith(handler).GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

    // A deliberate cancellation must not come back as a timeout, which is a result a caller would act on.
    [Fact]
    public async Task Get_WhenTheCallerCancels_ThrowsRatherThanReportingATimeout()
    {
        using var cancellation = new CancellationTokenSource();

        var pending = ClientWith(new NeverRespondingHandler()).GetAsync<HealthResult>("health", cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    // before the content-type check this body reached ReadFromJsonAsync
    [Fact]
    public async Task Get_WhenAProxyAnswersWithHtml_ReportsUnexpectedResponse()
    {
        var response = await Get(new CannedResponseHandler(HttpStatusCode.BadGateway, "<html>502</html>", "text/html"));

        Assert.False(response.Success);
        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, response.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenTheServerAnswersABare401_ReportsNotSignedIn()
    {
        var response = await Get(new CannedResponseHandler(HttpStatusCode.Unauthorized, string.Empty, "text/plain"));

        Assert.Equal(DesktopErrorCodes.NotSignedIn, response.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenTheServerAnswersABare403_ReportsNotAllowed()
    {
        var response = await Get(new CannedResponseHandler(HttpStatusCode.Forbidden, string.Empty, "text/plain"));

        Assert.Equal(DesktopErrorCodes.NotAllowed, response.Error!.Code);
    }

    // what [ApiController] produced before ModelStateEnvelope: no Error, so this read as success
    [Fact]
    public async Task Get_WhenTheServerAnswersProblemDetails_DoesNotReadItAsSuccess()
    {
        var response = await Get(new CannedResponseHandler(
            HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400,"traceId":"00-abc-def-00"}""",
            "application/problem+json"));

        Assert.False(response.Success);
        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, response.Error!.Code);
    }

    // a refusal the API itself sent still arrives intact, status code notwithstanding
    [Fact]
    public async Task Get_WhenTheApiRefusesWithTheEnvelope_KeepsTheServerCode()
    {
        var response = await Get(new CannedResponseHandler(
            HttpStatusCode.NotFound,
            """{"error":{"code":"error.unknown_permission"},"success":false}"""));

        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.UnknownPermission, response.Error!.Code);
    }
}
