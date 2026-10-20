using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Mosaic.Desktop.Tests;

// compares where a box draws the start and the end of its text, which is the order a person reads
internal static class TextOrder
{
    public static bool IsLeftToRight(TextBox box)
    {
        var layout = box.GetVisualDescendants().OfType<TextPresenter>().Single().TextLayout;

        return layout.HitTestTextPosition(0).X < layout.HitTestTextPosition(box.Text!.Length).X;
    }
}
