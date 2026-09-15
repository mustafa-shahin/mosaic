using Mosaic.Contracts;
using Mosaic.Domain.Errors;

namespace Mosaic.Application.Validation;

// Not beside the other refusals in Domain, because Domain references nothing at all and FieldError is
// a wire type. Same reason PermissionRegistry is not in Domain either.
public sealed class ValidationException(IReadOnlyDictionary<string, IReadOnlyList<FieldError>> fieldErrors)
    : MosaicException(ErrorCodes.ValidationFailed)
{
    public IReadOnlyDictionary<string, IReadOnlyList<FieldError>> FieldErrors { get; } = fieldErrors;
}