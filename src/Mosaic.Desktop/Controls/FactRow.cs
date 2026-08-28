using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mosaic.Desktop.Controls;

public class FactRow : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FactRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<FactRow, string?>(nameof(Value));

    public static readonly StyledProperty<bool> IsLastProperty =
        AvaloniaProperty.Register<FactRow, bool>(nameof(IsLast));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // Set on the last row to drop its bottom border.
    public bool IsLast
    {
        get => GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }
}
