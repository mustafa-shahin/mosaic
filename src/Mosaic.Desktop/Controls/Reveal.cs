using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Threading;

namespace Mosaic.Desktop.Controls;

// A Border that stays on the screen for a moment after it is closed, so that a style can move it out first.
[PseudoClasses(Open)]
public class Reveal : Border
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Reveal, bool>(nameof(IsOpen));

    public static readonly StyledProperty<TimeSpan> LeaveTimeProperty =
        AvaloniaProperty.Register<Reveal, TimeSpan>(nameof(LeaveTime));

    private const string Open = ":open";

    private IDisposable? _leaving;

    public Reveal()
    {
        IsVisible = false;
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    // how long the style takes to move it out; it is hidden only after that time
    public TimeSpan LeaveTime
    {
        get => GetValue(LeaveTimeProperty);
        set => SetValue(LeaveTimeProperty, value);
    }

    // a style written for a Border must also find this class
    protected override Type StyleKeyOverride => typeof(Border);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsOpenProperty)
        {
            return;
        }

        _leaving?.Dispose();
        _leaving = null;

        if (IsOpen)
        {
            IsVisible = true;
        }
        else
        {
            _leaving = DispatcherTimer.RunOnce(() => IsVisible = false, LeaveTime);
        }

        PseudoClasses.Set(Open, IsOpen);
    }
}
