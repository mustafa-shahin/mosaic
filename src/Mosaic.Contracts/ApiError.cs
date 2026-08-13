namespace Mosaic.Contracts;

public sealed record ApiError
{
    public required string Code { get; init; }
    public string? Message { get; init; }
    public IReadOnlyDictionary<string, string>? Params { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors { get; init; }
}
