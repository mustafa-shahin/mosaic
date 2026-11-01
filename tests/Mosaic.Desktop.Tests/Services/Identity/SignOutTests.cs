using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services.Identity;

public sealed class SignOutTests
{
    private readonly CurrentSession _session = new();
    private readonly List<SessionEnded> _ends = [];

    public SignOutTests()
    {
        _session.Ended += (_, ended) => _ends.Add(ended);
    }

    private SignOut SignOutWith(HttpMessageHandler handler) =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, _session)), _session);

    [Fact]
    public void Here_EndsTheSessionBeforeTheServerAnswers()
    {
        var started = _session.Start(SignInResults.Ada());
        var server = new NeverRespondingHandler();

        _ = SignOutWith(server).HereAsync();

        Assert.Null(_session.Current);
        Assert.Equal([new SessionEnded(started, SessionEndReason.SignedOut)], _ends);
        Assert.Equal(1, server.Calls);
    }

    [Fact]
    public async Task Here_TellsTheServerWithTheTokenOfTheSessionItEnded()
    {
        _session.Start(SignInResults.Ada("mosaic_leaving"));
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");

        await SignOutWith(recorder).HereAsync();

        Assert.Equal(ApiPaths.Auth + AuthRoutes.SignOut, recorder.Path);
        Assert.Equal("mosaic_leaving", recorder.Authorization?.Parameter);
    }

    // a row the server never deleted still ends at its expiry, so neither answer may bring anything back
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Here_WhateverTheServerAnswers_StaysSignedOutWithOneEnd(HttpStatusCode status)
    {
        var started = _session.Start(SignInResults.Ada());

        await SignOutWith(new CannedResponseHandler(status, string.Empty, "text/plain")).HereAsync();

        Assert.Null(_session.Current);
        Assert.Equal([new SessionEnded(started, SessionEndReason.SignedOut)], _ends);
    }

    [Fact]
    public async Task Here_WithoutASession_SendsNothing()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");

        await SignOutWith(recorder).HereAsync();

        Assert.Equal(0, recorder.Calls);
        Assert.Empty(_ends);
    }

    [Fact]
    public async Task Everywhere_WhenTheServerConfirms_EndsTheSession()
    {
        var started = _session.Start(SignInResults.Ada("mosaic_everywhere"));
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");

        var answer = await SignOutWith(recorder).EverywhereAsync(TestContext.Current.CancellationToken);

        Assert.True(answer.Success);
        Assert.Equal(ApiPaths.Auth + AuthRoutes.SignOutEverywhere, recorder.Path);
        Assert.Equal("mosaic_everywhere", recorder.Authorization?.Parameter);
        Assert.Equal([new SessionEnded(started, SessionEndReason.SignedOutEverywhere)], _ends);
    }

    // the stamp may be unchanged, so the person must see that it did not happen
    [Fact]
    public async Task Everywhere_WhenTheServerFails_KeepsTheSessionAndReturnsTheReason()
    {
        var started = _session.Start(SignInResults.Ada());

        var answer = await SignOutWith(new CannedResponseHandler(HttpStatusCode.InternalServerError, """{"error":{"code":"error.unexpected"}}"""))
            .EverywhereAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Unexpected, answer.Error!.Code);
        Assert.Same(started, _session.Current);
        Assert.Empty(_ends);
    }

    [Fact]
    public async Task Everywhere_WhenTheServerCannotBeReached_KeepsTheSession()
    {
        var started = _session.Start(SignInResults.Ada());

        var answer = await SignOutWith(new ThrowingHandler(new HttpRequestException("refused")))
            .EverywhereAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopErrorCodes.ApiUnreachable, answer.Error!.Code);
        Assert.Same(started, _session.Current);
    }

    // the token was already dead, so the session ends the way any refused request ends it
    [Fact]
    public async Task Everywhere_WhenTheTokenIsRefused_EndsTheSessionAsRefused()
    {
        var started = _session.Start(SignInResults.Ada());

        await SignOutWith(new CannedResponseHandler(HttpStatusCode.Unauthorized, string.Empty, "text/plain"))
            .EverywhereAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new SessionEnded(started, SessionEndReason.Refused)], _ends);
    }
}
