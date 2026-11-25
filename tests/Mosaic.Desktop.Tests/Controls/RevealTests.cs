using Avalonia.Controls;
using Avalonia.Threading;
using Mosaic.Desktop.Controls;

namespace Mosaic.Desktop.Tests.Controls;

public sealed class RevealTests
{
    private static readonly TimeSpan LeaveTime = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void AClosedReveal_IsHidden()
    {
        Assert.False(Shown().IsVisible);
    }

    [Fact]
    public void Opening_ShowsItAtOnce()
    {
        var reveal = Shown();

        reveal.IsOpen = true;

        Assert.True(reveal.IsVisible);
        Assert.Contains(":open", reveal.Classes);
    }

    // a style moves it out while it closes, so it stays on the screen until the leave time is over
    [Fact]
    public async Task Closing_HidesItOnlyAfterTheLeaveTime()
    {
        var reveal = Shown();

        reveal.IsOpen = true;
        reveal.IsOpen = false;
        await Pump(LeaveTime / 6);

        Assert.True(reveal.IsVisible);
        Assert.DoesNotContain(":open", reveal.Classes);

        await Until(() => !reveal.IsVisible);

        Assert.False(reveal.IsVisible);
    }

    [Fact]
    public async Task OpeningAgainWhileItLeaves_KeepsItOnTheScreen()
    {
        var reveal = Shown();

        reveal.IsOpen = true;
        reveal.IsOpen = false;
        reveal.IsOpen = true;

        await Pump(LeaveTime * 3);

        Assert.True(reveal.IsVisible);
    }

    private static Reveal Shown()
    {
        var reveal = new Reveal { LeaveTime = LeaveTime, Child = new TextBlock { Text = "details" } };
        var window = new Window { Width = 400, Height = 300, Content = reveal };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return reveal;
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

    private static async Task Pump(TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < until)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
