using Avalonia;
using Avalonia.Controls.Primitives;

namespace Mosaic.Desktop.Controls;

// The round mark before a person's name. With no initials it is a dashed empty circle, for nobody known.
public class PersonBadge : TemplatedControl
{
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<PersonBadge, string?>(nameof(Initials));

    public string? Initials
    {
        get => GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }
}
