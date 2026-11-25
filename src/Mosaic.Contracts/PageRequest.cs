namespace Mosaic.Contracts;

// which part of a list a caller asks for
public sealed record PageRequest
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
