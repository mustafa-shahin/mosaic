using Mosaic.Contracts;
using Mosaic.Domain.Errors;

namespace Mosaic.Api.Validation;

// Not in Domain with the other refusals, because Domain references nothing and FieldError is a wire type.
public sealed class ValidationException(IReadOnlyDictionary<string, IReadOnlyList<FieldError>> fieldErrors)
    : MosaicException(ErrorCodes.ValidationFailed)
{
    public IReadOnlyDictionary<string, IReadOnlyList<FieldError>> FieldErrors { get; } = fieldErrors;
}