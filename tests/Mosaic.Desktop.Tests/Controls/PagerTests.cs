using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Desktop.Controls;

namespace Mosaic.Desktop.Tests.Controls;

public sealed class PagerTests
{
    private readonly List<int> _asked = [];

    [Fact]
    public void Next_AsksForThePageAfter()
    {
        var (window, pager) = Shown(PagerMode.Steps, 2, 5);

        Click(window, Part(pager, "PART_Next"));

        Assert.Equal([3], _asked);
    }

    [Fact]
    public void Previous_AsksForThePageBefore()
    {
        var (window, pager) = Shown(PagerMode.Steps, 2, 5);

        Click(window, Part(pager, "PART_Previous"));

        Assert.Equal([1], _asked);
    }

    // the theme switches the button off at each end, or the first page would ask for page 0
    [Fact]
    public void AtTheEnds_PreviousAndNextAskForNothing()
    {
        var (first, onFirst) = Shown(PagerMode.Steps, 1, 5);
        var (last, onLast) = Shown(PagerMode.Steps, 5, 5);

        Click(first, Part(onFirst, "PART_Previous"));
        Click(last, Part(onLast, "PART_Next"));

        Assert.Empty(_asked);
    }

    [Fact]
    public void ANumber_AsksForItsPage()
    {
        var (window, pager) = Shown(PagerMode.Numbers, 1, 5);

        Click(window, NumberButton(pager, 3));

        Assert.Equal([3], _asked);
    }

    [Fact]
    public void TheCurrentPagesNumber_AsksForNothing()
    {
        var (window, pager) = Shown(PagerMode.Numbers, 2, 5);

        Click(window, NumberButton(pager, 2));

        Assert.Empty(_asked);
    }

    [Theory]
    [InlineData(1, 3, true, false)]
    [InlineData(2, 3, false, false)]
    [InlineData(3, 3, false, true)]
    [InlineData(1, 1, true, true)]
    public void TheEnds_AreMarkedForTheTheme(int current, int total, bool first, bool last)
    {
        var (_, pager) = Shown(PagerMode.Numbers, current, total);

        Assert.Equal(first, pager.Classes.Contains(":first"));
        Assert.Equal(last, pager.Classes.Contains(":last"));
    }

    // bindings may set the page before the count, so a change of either redraws the numbers
    [Fact]
    public void TheNumbers_FollowThePageAndTheCount()
    {
        var (_, pager) = Shown(PagerMode.Numbers, 1, 3);

        pager.CurrentPage = 5;
        pager.TotalPages = 10;

        Assert.Equal(PageSlots.For(5, 10), pager.Slots);
        Assert.DoesNotContain(":last", pager.Classes);
    }

    private (Window Window, Pager Pager) Shown(PagerMode mode, int current, int total)
    {
        var pager = new Pager
        {
            Mode = mode,
            CurrentPage = current,
            TotalPages = total,
            PreviousText = "Previous",
            NextText = "Next",
            GoToCommand = new RelayCommand<int>(_asked.Add),
        };
        var window = new Window { Width = 1360, Height = 200, Content = pager };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, pager);
    }

    private static Button Part(Pager pager, string name) =>
        pager.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static Button NumberButton(Pager pager, int number) =>
        pager.GetVisualDescendants().OfType<Button>().Single(button => button.DataContext is PageSlot slot && slot.Number == number);

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
