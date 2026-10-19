namespace Mosaic.Contracts;

// Params of its own, so two fields refused with the same code can each carry their own number.
public sealed record FieldError
{
    public required string Code { get; init; }
    public IReadOnlyDictionary<string, string>? Params { get; init; }
}
