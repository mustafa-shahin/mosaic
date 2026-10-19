using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mosaic.Desktop.Controls;

public class ErrorBox : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ErrorBox, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
