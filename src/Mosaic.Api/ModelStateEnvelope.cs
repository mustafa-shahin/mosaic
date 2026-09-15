using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Mosaic.Contracts;

namespace Mosaic.Api;

// [ApiController] answers a failed model check with its own ValidationProblemDetails, which the
// desktop read as an envelope reporting success. Same shape as ValidationException instead.
internal static class ModelStateEnvelope
{
    // An attribute names its code; anything else in ModelState is the binder's English.
    private const string CodePrefix = "error.";

    public static IActionResult Build(ActionContext context)
    {
        var byField = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            var errors = new List<FieldError>(entry.Errors.Count);

            foreach (var error in entry.Errors)
            {
                errors.Add(new FieldError { Code = CodeOf(error) });
            }

            byField[FieldName(key)] = errors;
        }

        var response = ApiResponse.Fail(new ApiError
        {
            Code = ErrorCodes.ValidationFailed,
            FieldErrors = byField
        }) with
        {
            TraceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
        };

        return new BadRequestObjectResult(response);
    }

    // ErrorMessage is null when the entry carries an Exception instead, which is the binder failing.
    private static string CodeOf(ModelError error) =>
        error.ErrorMessage is { } message && message.StartsWith(CodePrefix, StringComparison.Ordinal)
            ? message
            : ErrorCodes.InvalidValue;

    // "Email" from an attribute, "$.email" from the JSON binder. The desktop keys fields camelCase.
    private static string FieldName(string key)
    {
        var name = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;

        return JsonNamingPolicy.CamelCase.ConvertName(name);
    }
}
