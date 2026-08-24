using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mosaic.Api;
using Mosaic.Contracts;
using Mosaic.Domain.Errors;

namespace Mosaic.Integration.Tests;

public sealed class MosaicExceptionHandlerTests
{
    // Environments.Production isn't a const, so it can't be a default parameter.
    private const string Production = "Production";
    private const string Development = "Development";

    private static async Task<(int Status, ApiResponse Body)> Handle(
        Exception exception, string environmentName = Production)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new MosaicExceptionHandler(
            NullLogger<MosaicExceptionHandler>.Instance, new StubEnvironment(environmentName));

        var handled = await handler.TryHandleAsync(
            context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiResponse>(
            context.Response.Body, MosaicJson.Options, TestContext.Current.CancellationToken);

        return (context.Response.StatusCode, body!);
    }

    [Fact]
    public async Task TryHandle_ForARefusal_KeepsItsCodeDetailAndStatus()
    {
        var (status, body) = await Handle(new NotFoundException("user.not_found", "no user 7"));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.False(body.Success);
        Assert.Equal("user.not_found", body.Error!.Code);
        Assert.Equal("no user 7", body.Error.Message);
    }

    [Fact]
    public async Task TryHandle_ForARefusalTheHandlerHasNeverSeen_TakesTheStatusFromTheException()
    {
        var (status, body) = await Handle(new QuotaExceededException());

        Assert.Equal(StatusCodes.Status429TooManyRequests, status);
        Assert.Equal("quota.exceeded", body.Error!.Code);
    }

    private sealed class QuotaExceededException() : MosaicException("quota.exceeded")
    {
        public override HttpStatusCode Status => HttpStatusCode.TooManyRequests;
    }

    [Fact]
    public async Task TryHandle_ForAnUnmappedException_IsFiveHundredAndSaysNothingAboutItself()
    {
        var (status, body) = await Handle(new InvalidOperationException("table 'users' does not exist"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal(ErrorCodes.Unexpected, body.Error!.Code);
        Assert.Null(body.Error.Message);
    }

    [Fact]
    public async Task TryHandle_InDevelopment_KeepsTheDetailSoTheCauseIsVisible()
    {
        var (_, body) = await Handle(
            new InvalidOperationException("table 'users' does not exist"), Development);

        Assert.Equal("table 'users' does not exist", body.Error!.Message);
    }

    [Fact]
    public async Task TryHandle_ForAnyFailure_CarriesATraceId()
    {
        var (_, body) = await Handle(new NotFoundException("user.not_found"));

        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task TryHandle_WhenTheClientWentAway_WritesNothingAndLogsNoError()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.RequestAborted = new CancellationToken(canceled: true);
        var logger = new RecordingLogger();

        var handled = await new MosaicExceptionHandler(logger, new StubEnvironment(Production))
            .TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task TryHandle_WhenTheResponseHasStarted_LeavesItAlone()
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(new StartedResponse());

        var context = new DefaultHttpContext(features);

        var handled = await new MosaicExceptionHandler(
                NullLogger<MosaicExceptionHandler>.Instance, new StubEnvironment(Production))
            .TryHandleAsync(context, new InvalidOperationException("too late"), CancellationToken.None);

        Assert.False(handled);
    }

    private sealed class RecordingLogger : ILogger<MosaicExceptionHandler>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Mosaic.Api";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
