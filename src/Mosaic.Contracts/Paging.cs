namespace Mosaic.Contracts;

// what the server and the desktop must agree on for every list that comes in pages
public static class Paging
{
    public const string PageParameter = "page";
    public const string PageSizeParameter = "pageSize";

    public const int FirstPage = 1;

    // a caller who names no size gets the default, and no caller gets more rows in one answer than the largest size
    public const int DefaultPageSize = 20;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    // at the largest size the rows before this page still fit in an int, so the number of rows to skip never turns negative
    public const int MaxPage = int.MaxValue / MaxPageSize;
}
