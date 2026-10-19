using CommunityToolkit.Mvvm.ComponentModel;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell;

// What the shell shows when this user may see no entry at all.
public sealed class NoAccessViewModel(Localizer strings) : ObservableObject
{
    public string Message { get; } = strings.Translate("shell.no_access");
}