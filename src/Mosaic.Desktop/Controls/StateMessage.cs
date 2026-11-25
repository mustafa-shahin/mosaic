using Avalonia;
using Avalonia.Controls;
using FluentIcons.Common;

namespace Mosaic.Desktop.Controls;

// Stands in place of a screen's content when there is nothing to show. The content is an optional button.
public class StateMessage : ContentControl
{
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<StateMessage, Symbol>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StateMessage, string?>(nameof(Title));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StateMessage, string?>(nameof(Text));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // one sentence under the title: what would appear here, or what to do
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
