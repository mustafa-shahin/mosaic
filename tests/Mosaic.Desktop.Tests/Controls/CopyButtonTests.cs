using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Desktop.Controls;

namespace Mosaic.Desktop.Tests.Controls;

public sealed class CopyButtonTests
{
    [Fact]
    public async Task AClick_CopiesTheText_AndShowsTheMark()
    {
        var (window, button) = await Shown("203.0.113.7");

        Press(window, button);
        await Until(() => button.Classes.Contains(":copied"));

        Assert.Equal("203.0.113.7", await window.Clipboard!.TryGetTextAsync());
        Assert.Contains(":copied", button.Classes);
    }

    [Fact]
    public async Task ANewText_TakesTheMarkAway()
    {
        var (window, button) = await Shown("203.0.113.7");

        Press(window, button);
        await Until(() => button.Classes.Contains(":copied"));

        button.Text = "198.51.100.44";

        Assert.DoesNotContain(":copied", button.Classes);
    }

    [Fact]
    public async Task WithNoText_AClickCopiesNothing()
    {
        var (window, button) = await Shown(null);

        Press(window, button);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("before", await window.Clipboard!.TryGetTextAsync());
        Assert.DoesNotContain(":copied", button.Classes);
    }

    // the clipboard starts with a text of its own, so a copy that did not happen cannot pass for one
    private static async Task<(Window Window, CopyButton Button)> Shown(string? text)
    {
        var button = new CopyButton { Text = text };
        var window = new Window { Width = 400, Height = 200, Content = button };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        await window.Clipboard!.SetTextAsync("before");

        return (window, button);
    }

    // the control fills the window and its button sits at the start of it
    private static void Press(Window window, CopyButton copy) =>
        Click(window, copy.GetVisualDescendants().OfType<Button>().Single());

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < until)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
