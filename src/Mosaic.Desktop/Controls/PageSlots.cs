namespace Mosaic.Desktop.Controls;

// Decides which page numbers a pager shows, and where it leaves a gap.
public static class PageSlots
{
    // how many pages show on each side of the current page
    private const int Neighbours = 2;

    public static IReadOnlyList<PageSlot> For(int current, int total)
    {
        var slots = new List<PageSlot>();

        for (var page = 1; page <= total; page++)
        {
            if (page == 1 || page == total || Math.Abs(page - current) <= Neighbours)
            {
                slots.Add(new PageSlot(page, page == current));
            }
            else if (slots.LastOrDefault()?.IsGap == false)
            {
                // a run of hidden pages gets one gap, not one for each page
                slots.Add(PageSlot.Gap);
            }
        }

        return slots;
    }
}
