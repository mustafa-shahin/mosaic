using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Mosaic.Api.Validation;
using Mosaic.Contracts;
using Mosaic.Domain.Errors;

namespace Mosaic.Api;

public sealed partial class MosaicExceptionHandler(
    ILogger<MosaicExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    // System.Text.Json's path to a property of the body, "$.email"; the body itself is "$"
    private const string BodyPropertyPrefix = "$.";

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

        // a request the endpoint could not read is the client's mistake, so it is refused like a broken rule
        if (exception is BadHttpRequestException unreadable)
        {
            LogRefusal(logger, ErrorCodes.ValidationFailed);

            return new ApiError
            {
                Code = ErrorCodes.ValidationFailed,
                FieldErrors = FieldThatBroke(unreadable)
            };
        }

        // the middleware logs no exception that a handler reports as handled
        LogUnhandled(logger, exception);

        return new ApiError
        {
            Code = ErrorCodes.Unexpected,
            // exception messages can leak table names, paths, connection strings
            Message = environment.IsDevelopment() ? exception.Message : null
        };
    }

    // a MosaicException kind with no line here answers 500, and ExceptionStatusTests fails on it
    internal static int StatusFor(Exception exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        InvalidRequestException => StatusCodes.Status400BadRequest,
        NotPermittedException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        UnavailableException => StatusCodes.Status503ServiceUnavailable,
        // keeps the status ASP.NET chose for it
        BadHttpRequestException unreadable => unreadable.StatusCode,
        _ => StatusCodes.Status500InternalServerError,
    };

    // only the JSON reader knows where the body broke, so an empty body names no field
    private static Dictionary<string, IReadOnlyList<FieldError>>? FieldThatBroke(BadHttpRequestException exception)
    {
        if (exception.InnerException is not JsonException { Path: { } path })
        {
            return null;
        }

        var property = path.StartsWith(BodyPropertyPrefix, StringComparison.Ordinal) ? path[BodyPropertyPrefix.Length..] : path;

        // the desktop keys its fields camelCase, as the validators name them
        return new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal)
        {
            [JsonNamingPolicy.CamelCase.ConvertName(property)] = [new FieldError { Code = ErrorCodes.InvalidValue }],
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request refused with {Code}")]
    private static partial void LogRefusal(ILogger logger, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}