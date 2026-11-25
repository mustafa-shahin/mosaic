using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Mosaic.Desktop.Controls;

public enum PagerMode
{
    Steps,
    Numbers
}

// The foot of a list: where the person is in it, and the buttons that ask for another page.
[PseudoClasses(First, Last)]
public class Pager : TemplatedControl
{
    public static readonly StyledProperty<PagerMode> ModeProperty =
        AvaloniaProperty.Register<Pager, PagerMode>(nameof(Mode));

    public static readonly DirectProperty<Pager, IReadOnlyList<PageSlot>> SlotsProperty =
        AvaloniaProperty.RegisterDirect<Pager, IReadOnlyList<PageSlot>>(nameof(Slots), pager => pager.Slots);

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<Pager, int>(nameof(CurrentPage), 1);

    public static readonly StyledProperty<int> TotalPagesProperty =
        AvaloniaProperty.Register<Pager, int>(nameof(TotalPages), 1);

    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<Pager, string?>(nameof(Summary));

    public static readonly StyledProperty<string?> PositionProperty =
        AvaloniaProperty.Register<Pager, string?>(nameof(Position));

    public static readonly StyledProperty<string?> PreviousTextProperty =
        AvaloniaProperty.Register<Pager, string?>(nameof(PreviousText));

    public static readonly StyledProperty<string?> NextTextProperty =
        AvaloniaProperty.Register<Pager, string?>(nameof(NextText));

    public static readonly StyledProperty<ICommand?> GoToCommandProperty =
        AvaloniaProperty.Register<Pager, ICommand?>(nameof(GoToCommand));

    private const string First = ":first";
    private const string Last = ":last";
    private const string PreviousPart = "PART_Previous";
    private const string NextPart = "PART_Next";

    private IReadOnlyList<PageSlot> _slots = [];

    public Pager()
    {
        // the click of a button inside the template travels up to this control
        AddHandler(Button.ClickEvent, Go);
        Follow();
    }

    // Steps shows Previous, the position and Next. Numbers shows a button for each page number in place of the position.
    public PagerMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    // the row of page numbers for the Numbers mode, made again whenever the page or the number of pages changes
    public IReadOnlyList<PageSlot> Slots
    {
        get => _slots;
        private set => SetAndRaise(SlotsProperty, ref _slots, value);
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int TotalPages
    {
        get => GetValue(TotalPagesProperty);
        set => SetValue(TotalPagesProperty, value);
    }

    // the finished text at the start of the foot, such as "Showing 51 to 100 of 1,284"
    public string? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    // the finished text between the two buttons, such as "Page 2 of 26"
    public string? Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public string? PreviousText
    {
        get => GetValue(PreviousTextProperty);
        set => SetValue(PreviousTextProperty, value);
    }

    public string? NextText
    {
        get => GetValue(NextTextProperty);
        set => SetValue(NextTextProperty, value);
    }

    // receives the number of the page the person asked for
    public ICommand? GoToCommand
    {
        get => GetValue(GoToCommandProperty);
        set => SetValue(GoToCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CurrentPageProperty || change.Property == TotalPagesProperty)
        {
            Follow();
        }
    }

    // what depends on the page: the two ends, where the theme switches a button off, and the row of numbers
    private void Follow()
    {
        PseudoClasses.Set(First, CurrentPage <= 1);
        PseudoClasses.Set(Last, CurrentPage >= TotalPages);

        Slots = PageSlots.For(CurrentPage, TotalPages);
    }

    private void Go(object? sender, RoutedEventArgs e)
    {
        var page = e.Source switch
        {
            Button { Name: PreviousPart } => CurrentPage - 1,
            Button { Name: NextPart } => CurrentPage + 1,
            Button { DataContext: PageSlot { Number: { } number } } => number,
            _ => CurrentPage,
        };

        if (page != CurrentPage && GoToCommand?.CanExecute(page) == true)
        {
            GoToCommand.Execute(page);
        }
    }
}
