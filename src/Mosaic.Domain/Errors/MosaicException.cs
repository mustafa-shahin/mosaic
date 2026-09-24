namespace Mosaic.Domain.Errors;

// Expected failures as opposed to bugs. The client translates Code itself, Detail is only there
// for debugging and is never shown to the user.
// No status code here: which HTTP status a kind maps to is the API's decision, see MosaicExceptionHandler.
public abstract class MosaicException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : Exception(detail ?? code)
{
    public string Code { get; } = code;

    public string? Detail { get; } = detail;

    // Placeholder values for the client's translation of Code.
    public IReadOnlyDictionary<string, string>? Parameters { get; } = parameters;
}

public sealed class NotFoundException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : MosaicException(code, detail, parameters);

public sealed class InvalidRequestException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : MosaicException(code, detail, parameters);

public sealed class NotPermittedException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : MosaicException(code, detail, parameters);
