using CommunityToolkit.Mvvm.ComponentModel;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell;

// An active account holding no role would otherwise open onto an empty window. This is the only
// screen the shell may name, because it belongs to the shell and not to a feature.
public sealed class NoAccessViewModel(Localizer strings) : ObservableObject
{
    public string Message { get; } = strings.Translate("shell.no_access");
}