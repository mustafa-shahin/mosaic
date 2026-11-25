namespace Mosaic.Contracts.Tests;

public sealed class PageResultTests
{
    private static PageResult<string> Page(int totalItems, int currentPage) => new()
    {
        Items = [],
        TotalItems = totalItems,
        ItemsPerPage = Paging.DefaultPageSize,
        CurrentPage = currentPage,
        IsEmptyWithoutFilters = false,
    };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(45, 3)]
    public void TotalPages_CountsAPartlyFilledPageAsAPage(int totalItems, int totalPages)
    {
        Assert.Equal(totalPages, Page(totalItems, Paging.FirstPage).TotalPages);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    [InlineData(3, true, false)]
    public void ThePagesBeforeAndAfter_ExistOnlyInsideTheList(int currentPage, bool hasPrevious, bool hasNext)
    {
        var page = Page(45, currentPage);

        Assert.Equal(hasPrevious, page.HasPreviousPage);
        Assert.Equal(hasNext, page.HasNextPage);
    }
}
