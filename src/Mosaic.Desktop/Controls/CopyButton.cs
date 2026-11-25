using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Mosaic.Desktop.Controls;

// A small button that copies its text. A check mark takes the place of its icon for a moment, so the person sees that it worked.
[PseudoClasses(Copied)]
public class CopyButton : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Text));

    private const string Copied = ":copied";

    private static readonly TimeSpan MarkTime = TimeSpan.FromSeconds(1.5);

    private IDisposable? _mark;

    public CopyButton()
    {
        // the click of the button inside the template travels up to this control
        AddHandler(Button.ClickEvent, Copy);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // the mark belongs to the text that was copied, so it must not stay beside the next one
        if (change.Property == TextProperty)
        {
            Mark(false);
        }
    }

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(Text);
        }
        catch (Exception)
        {
            // another program can hold the clipboard. Nothing was copied, so no mark, and the app stays open
            return;
        }

        Mark(true);
    }

    private void Mark(bool copied)
    {
        _mark?.Dispose();
        _mark = copied ? DispatcherTimer.RunOnce(() => Mark(false), MarkTime) : null;

        PseudoClasses.Set(Copied, copied);
    }
}
