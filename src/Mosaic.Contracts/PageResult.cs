namespace Mosaic.Contracts;

// one page of a list, with the total so the screen can show how many pages exist
public sealed record PageResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalItems { get; init; }
    public required int ItemsPerPage { get; init; }
    public required int CurrentPage { get; init; }

    // true when the list holds nothing at all, even without the filters of the request
    public required bool IsEmptyWithoutFilters { get; init; }

    public int TotalPages => (int)Math.Ceiling((double)TotalItems / ItemsPerPage);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}
