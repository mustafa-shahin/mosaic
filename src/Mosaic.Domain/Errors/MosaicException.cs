using System.Net;

namespace Mosaic.Domain.Errors;

// Expected failures as opposed to bugs. The client translates Code itself, Detail is only there
// for debugging and is never shown to the user.
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

    // every subclass has to pick one
    public abstract HttpStatusCode Status { get; }
}

public sealed class NotFoundException(
    string code,
    string? detail = null,
    IReadOnlyDictionary<string, string>? parameters = null)
    : MosaicException(code, detail, parameters)
{
    public override HttpStatusCode Status => HttpStatusCode.NotFound;
}
