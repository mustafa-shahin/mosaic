namespace Mosaic.Desktop.Shell.Navigation;

// What the sidebar is built from: the headings in their order and one contribution per screen, listed by hand.
public sealed record DesktopComposition(
    IReadOnlyList<NavSectionDefinition> Sections, IReadOnlyList<NavContribution> Contributions);
