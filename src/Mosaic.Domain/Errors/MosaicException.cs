namespace Mosaic.Domain.Errors;

// For expected failures: the client translates Code, and Detail is for debugging and never shown to a user.
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

// the server itself cannot take the request now, nothing about it was wrong
public sealed class UnavailableException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : MosaicException(code, detail, parameters);
