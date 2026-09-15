using Avalonia.Controls.Templates;

namespace Mosaic.Desktop.Shell.Navigation;

// Everything an edition adds to the shell. Core's lives next to App; pro's wraps core's and appends.
// There is no container and no assembly scanning, so the list is written by hand, once per edition.
public interface IDesktopComposition
{
    IReadOnlyList<NavSectionDefinition> Sections { get; }

    IReadOnlyList<NavContribution> Contributions { get; }

    // A screen needs a view. Core's templates stay in App.axaml; pro supplies its own through here.
    IReadOnlyList<IDataTemplate> Templates { get; }
}