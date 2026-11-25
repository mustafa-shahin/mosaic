using Avalonia;
using Avalonia.Controls;

namespace Mosaic.Desktop.Controls;

// a dropdown in a filter row: the closed box shows its label before the chosen value, such as "Action  All"
public class FilterBox : ComboBox
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FilterBox, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
