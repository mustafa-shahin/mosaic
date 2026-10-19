using System.Globalization;
using System.Text.Json;
using FluentValidation.Results;
using Mosaic.Contracts;

namespace Mosaic.Application.Validation;

public static class ValidationResultExtensions
{
    public static void ThrowIfInvalid(this ValidationResult result)
    {
        if (result.IsValid)
        {
            return;
        }

        var byField = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        foreach (var group in result.Errors.GroupBy(failure => FieldName(failure.PropertyName)))
        {
            byField[group.Key] = group
                .Select(failure => new FieldError { Code = failure.ErrorCode, Params = Limit(failure) })
                .ToList();
        }

        throw new ValidationException(byField);
    }

    // a length failure carries both bounds, the code says which one is the limit
    private static Dictionary<string, string>? Limit(ValidationFailure failure)
    {
        var bound = failure.ErrorCode switch
        {
            ErrorCodes.TooShort => LengthPlaceholders.Min,
            ErrorCodes.TooLong => LengthPlaceholders.Max,
            _ => null,
        };

        if (bound is null
            || failure.FormattedMessagePlaceholderValues is not { } placeholders
            || !placeholders.TryGetValue(bound, out var value)
            || value is not int limit)
        {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ErrorParams.Limit] = limit.ToString(CultureInfo.InvariantCulture),
        };
    }

    // field names go out camelCase, like every other name on the wire
    private static string FieldName(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);
}