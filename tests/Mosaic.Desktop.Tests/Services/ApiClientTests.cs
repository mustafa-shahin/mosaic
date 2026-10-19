using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Health;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services;

public sealed class ApiClientTests
{
    private const string SignOutPath = "v1/auth/sign-out";

    private static ApiClient ClientWith(HttpMessageHandler handler) => ClientWith(handler, new CurrentSession());

    private static ApiClient ClientWith(HttpMessageHandler handler, CurrentSession session) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, session);

    private static Task<ApiResponse<HealthResult>> Get(HttpMessageHandler handler) =>
        ClientWith(handler).GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

    private static List<SessionEnded> EndsOf(CurrentSession session)
    {
        var ends = new List<SessionEnded>();

        session.Ended += (_, ended) => ends.Add(ended);

        return ends;
    }

    [Fact]
    public async Task Get_WhenTheCallerCancels_ThrowsRatherThanReportingATimeout()
    {
        using var cancellation = new CancellationTokenSource();

        var pending = ClientWith(new NeverRespondingHandler()).GetAsync<HealthResult>("health", cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    // the content-type check stops this body before ReadFromJsonAsync reads it
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

    // ProblemDetails has no error member, so only its failed status keeps it from reading as success
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

    // the 404 leaves the envelope's code alone; the status only fills in when the body has no error
    [Fact]
    public async Task Get_WhenTheApiRefusesWithTheEnvelope_KeepsTheServerCode()
    {
        var response = await Get(new CannedResponseHandler(
            HttpStatusCode.NotFound,
            """{"error":{"code":"error.unknown_permission"},"success":false}"""));

        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.UnknownPermission, response.Error!.Code);
    }

    // camelCase and no nulls, because the API binds the body with the same options
    [Fact]
    public async Task Post_SendsTheBodyAsTheApiReadsIt()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, SignInBodies.Accepted);

        await ClientWith(recorder).PostAsync<SignInRequest, SignInResult>(
            "v1/auth/sign-in", new SignInRequest { Email = "ada@example.com" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/sign-in", recorder.Path);
        Assert.Equal("""{"email":"ada@example.com"}""", recorder.Body);
    }

    [Fact]
    public async Task Post_WhenTheApiAccepts_ReturnsWhatItSent()
    {
        var response = await ClientWith(new CannedResponseHandler(HttpStatusCode.OK, SignInBodies.Accepted))
            .PostAsync<SignInRequest, SignInResult>(
                "v1/auth/sign-in", new SignInRequest(), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal("Ada Lovelace", response.Data!.Session.FullName);
    }

    [Fact]
    public async Task Post_WhenTheApiRefusesWithTheEnvelope_KeepsTheServerCode()
    {
        var response = await ClientWith(new CannedResponseHandler(HttpStatusCode.Forbidden, SignInBodies.WrongPassword))
            .PostAsync<SignInRequest, SignInResult>(
                "v1/auth/sign-in", new SignInRequest(), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.InvalidCredentials, response.Error!.Code);
    }

    [Fact]
    public async Task PostWithoutData_WhenTheApiAccepts_IsASuccess()
    {
        var response = await ClientWith(new CannedResponseHandler(HttpStatusCode.Created, RegisterBodies.Owner))
            .PostAsync<RegisterRequest>("v1/auth/register", new RegisterRequest(), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
    }

    // a newer server may add data to this answer, and an installed desktop must still read it as the same success
    [Fact]
    public async Task PostWithoutData_WhenANewerServerAddsData_IsStillASuccess()
    {
        var response = await ClientWith(new CannedResponseHandler(HttpStatusCode.Created, """{"data":{"outcome":"Owner"},"success":true}"""))
            .PostAsync<RegisterRequest>("v1/auth/register", new RegisterRequest(), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task PostWithoutData_WhenTheApiRefusesWithTheEnvelope_KeepsTheCodeAndTheTraceId()
    {
        var response = await ClientWith(new CannedResponseHandler(
                HttpStatusCode.Forbidden,
                """{"error":{"code":"error.registration_closed"},"traceId":"00-abc-def-00","success":false}"""))
            .PostAsync<RegisterRequest>("v1/auth/register", new RegisterRequest(), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.RegistrationClosed, response.Error!.Code);
        Assert.Equal("00-abc-def-00", response.TraceId);
    }

    [Fact]
    public async Task ASession_SendsItsTokenAsABearer()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var session = new CurrentSession();

        session.Start(SignInResults.Ada("mosaic_the-token"));

        await ClientWith(recorder, session).GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", recorder.Authorization?.Scheme);
        Assert.Equal("mosaic_the-token", recorder.Authorization?.Parameter);
    }

    [Fact]
    public async Task NoSession_SendsNoAuthorization()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");

        await ClientWith(recorder).GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

        Assert.Equal(1, recorder.Calls);
        Assert.Null(recorder.Authorization);
    }

    // sign-out ends the session here first, so the request has to carry the one it names
    [Fact]
    public async Task PostWithoutBody_SendsTheSessionItIsGiven_AfterThatSessionEnded()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var session = new CurrentSession();
        var ended = session.Start(SignInResults.Ada("mosaic_ended-token"));

        session.End(ended, SessionEndReason.SignedOut);

        var response = await ClientWith(recorder, session).PostWithoutBodyAsync(
            SignOutPath, ended, TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/sign-out", recorder.Path);
        Assert.Null(recorder.Body);
        Assert.Equal("mosaic_ended-token", recorder.Authorization?.Parameter);
    }

    // HttpClient would send an absolute address past its base address, and the token with it
    [Fact]
    public async Task AnAbsoluteAddress_IsRefusedBeforeAnythingIsSent()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var session = new CurrentSession();

        session.Start(SignInResults.Ada());

        await Assert.ThrowsAsync<ArgumentException>(() => ClientWith(recorder, session)
            .GetAsync<HealthResult>("https://elsewhere.example/collect", TestContext.Current.CancellationToken));

        Assert.Equal(0, recorder.Calls);
    }

    [Fact]
    public async Task A401ToARequestWithTheToken_EndsThatSessionOnce()
    {
        var session = new CurrentSession();
        var started = session.Start(SignInResults.Ada());
        var ends = EndsOf(session);

        var response = await ClientWith(new CannedResponseHandler(HttpStatusCode.Unauthorized, string.Empty, "text/plain"), session)
            .GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

        Assert.Equal(DesktopErrorCodes.NotSignedIn, response.Error!.Code);
        Assert.Null(session.Current);
        Assert.Equal([new SessionEnded(started, SessionEndReason.Refused)], ends);
    }

    // ending the session disposes the screen that asked, which cancels its token, so the end has to come after the read
    [Fact]
    public async Task A401_WhoseSessionEndCancelsTheCaller_StillGetsItsAnswer()
    {
        using var screen = new CancellationTokenSource();
        var session = new CurrentSession();

        session.Start(SignInResults.Ada());
        session.Ended += (_, _) => screen.Cancel();

        var response = await ClientWith(
                new CannedResponseHandler(HttpStatusCode.Unauthorized, """{"error":{"code":"client.not_signed_in"}}"""), session)
            .GetAsync<HealthResult>("health", screen.Token);

        Assert.Equal(DesktopErrorCodes.NotSignedIn, response.Error!.Code);
        Assert.True(screen.IsCancellationRequested);
    }

    [Fact]
    public async Task A401WithoutAToken_EndsNothing()
    {
        var session = new CurrentSession();
        var ends = EndsOf(session);

        await ClientWith(new CannedResponseHandler(HttpStatusCode.Unauthorized, string.Empty, "text/plain"), session)
            .GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

        Assert.Empty(ends);
    }

    // the answer for the first sign-in comes back after the second one began
    [Fact]
    public async Task ALate401ForAnOlderSession_LeavesTheNewerOneAlone()
    {
        var session = new CurrentSession();
        var older = session.Start(SignInResults.Ada("mosaic_older"));

        session.End(older, SessionEndReason.SignedOut);

        var newer = session.Start(SignInResults.Ada("mosaic_newer"));
        var ends = EndsOf(session);

        await ClientWith(new CannedResponseHandler(HttpStatusCode.Unauthorized, string.Empty, "text/plain"), session)
            .PostWithoutBodyAsync(SignOutPath, older, TestContext.Current.CancellationToken);

        Assert.Same(newer, session.Current);
        Assert.Empty(ends);
    }

    // a 403 says this person may not do it, and signing in again would not change that
    [Fact]
    public async Task A403_EndsNoSession()
    {
        var session = new CurrentSession();
        var started = session.Start(SignInResults.Ada());
        var ends = EndsOf(session);

        await ClientWith(new CannedResponseHandler(HttpStatusCode.Forbidden, string.Empty, "text/plain"), session)
            .GetAsync<HealthResult>("health", TestContext.Current.CancellationToken);

        Assert.Same(started, session.Current);
        Assert.Empty(ends);
    }
}
