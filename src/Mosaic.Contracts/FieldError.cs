namespace Mosaic.Contracts;

// A code rather than a message, so the client translates it, and its own Params rather than the
// error's, so two fields failing the same rule can each carry their own number.
public sealed record FieldError
{
    public required string Code { get; init; }
    public IReadOnlyDictionary<string, string>? Params { get; init; }
}
