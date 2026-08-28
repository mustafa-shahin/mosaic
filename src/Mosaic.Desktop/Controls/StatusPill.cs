using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mosaic.Desktop.Controls;

public enum StatusLevel
{
    Info,
    Ok,
    Warn,
    Bad
}

public class StatusPill : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    public static readonly StyledProperty<StatusLevel> LevelProperty =
        AvaloniaProperty.Register<StatusPill, StatusLevel>(nameof(Level));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }
}
