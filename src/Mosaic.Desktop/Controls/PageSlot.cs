namespace Mosaic.Desktop.Controls;

// One place in a pager's row of numbers: a page, or a gap where pages are left out. A gap has no number.
public sealed record PageSlot(int? Number, bool IsCurrent)
{
    public static PageSlot Gap { get; } = new(null, false);

    public bool IsGap => Number is null;
}
