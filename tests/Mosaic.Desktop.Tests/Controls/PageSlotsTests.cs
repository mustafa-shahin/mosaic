using Mosaic.Desktop.Controls;

namespace Mosaic.Desktop.Tests.Controls;

public sealed class PageSlotsTests
{
    // the current page in brackets, a gap as three dots
    [Theory]
    [InlineData(1, 0, "")]
    [InlineData(1, 1, "[1]")]
    [InlineData(1, 4, "[1] 2 3 4")]
    [InlineData(1, 5, "[1] 2 3 ... 5")]
    [InlineData(3, 5, "1 2 [3] 4 5")]
    [InlineData(4, 7, "1 2 3 [4] 5 6 7")]
    [InlineData(5, 9, "1 ... 3 4 [5] 6 7 ... 9")]
    [InlineData(5, 10, "1 ... 3 4 [5] 6 7 ... 10")]
    [InlineData(10, 10, "1 ... 8 9 [10]")]
    public void For_ShowsBothEndsAndTwoPagesEachSideOfTheCurrent(int current, int total, string shown)
    {
        Assert.Equal(shown, string.Join(' ', PageSlots.For(current, total).Select(Describe)));
    }

    private static string Describe(PageSlot slot) =>
        slot switch
        {
            { IsGap: true } => "...",
            { IsCurrent: true } => $"[{slot.Number}]",
            _ => $"{slot.Number}",
        };
}
