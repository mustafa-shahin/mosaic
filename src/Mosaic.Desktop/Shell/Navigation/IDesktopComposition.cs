using Avalonia.Controls.Templates;

namespace Mosaic.Desktop.Shell.Navigation;

// Everything an edition adds to the shell, listed by hand. Pro's composition wraps core's and appends.
public interface IDesktopComposition
{
    IReadOnlyList<NavSectionDefinition> Sections { get; }

    IReadOnlyList<NavContribution> Contributions { get; }

    // A screen needs a view. Core's templates stay in App.axaml; pro supplies its own through here.
    IReadOnlyList<IDataTemplate> Templates { get; }
}