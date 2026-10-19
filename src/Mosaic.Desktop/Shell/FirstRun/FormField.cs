using CommunityToolkit.Mvvm.ComponentModel;

namespace Mosaic.Desktop.Shell.FirstRun;

// what was typed into one field, and the refusal shown under it
public sealed partial class FormField : ObservableObject
{
    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool HasError => Error is not null;
}
