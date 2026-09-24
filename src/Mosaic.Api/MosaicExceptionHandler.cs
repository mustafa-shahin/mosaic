using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Mosaic.Contracts;
using Mosaic.Domain.Errors;
using Mosaic.Application.Validation;

namespace Mosaic.Api;

public sealed partial class MosaicExceptionHandler(
    ILogger<MosaicExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // client went away, nothing to do
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        // too late to change the response
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusFor(exception);

        var response = ApiResponse.Fail(Describe(exception)) with
        {
            TraceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        await httpContext.Response.WriteAsJsonAsync(response, MosaicJson.Options, cancellationToken);

        return true;
    }

    private ApiError Describe(Exception exception)
    {
        // must come first: a ValidationException is a MosaicException, and matching that branch
        // instead would silently drop the field errors
        if (exception is ValidationException invalid)
        {
            LogRefusal(logger, invalid.Code);

            return new ApiError
            {
                Code = invalid.Code,
                FieldErrors = invalid.FieldErrors
            };
        }
        if (exception is MosaicException refusal)
        {
            LogRefusal(logger, refusal.Code);

            return new ApiError
            {
                Code = refusal.Code,
                Message = refusal.Detail,
                Params = refusal.Parameters
            };
        }

        // the middleware doesn't log exceptions we report as handled, so we have to
        LogUnhandled(logger, exception);

        return new ApiError
        {
            Code = ErrorCodes.Unexpected,
            // exception messages can leak table names, paths, connection strings
            Message = environment.IsDevelopment() ? exception.Message : null
        };
    }

    // The domain names the kind of a refusal; which status that is on the wire is decided here and
    // nowhere else. A new kind gets a line, and ExceptionStatusTests notices when it is forgotten.
    internal static int StatusFor(Exception exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        InvalidRequestException => StatusCodes.Status400BadRequest,
        NotPermittedException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status500InternalServerError,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Request refused with {Code}")]
    private static partial void LogRefusal(ILogger logger, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}