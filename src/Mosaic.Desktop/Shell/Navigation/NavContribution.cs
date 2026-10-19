using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// A heading in the sidebar. The composition declares them in the order they appear.
public sealed record NavSectionDefinition(string Key, string CaptionKey);

// One screen a feature offers the sidebar. Local shows it without the server listing its key.
public sealed record NavContribution(
    string SectionKey,
    string Key,
    Symbol Icon,
    bool Local,
    Func<Localizer, ObservableObject> Open,
    IReadOnlyList<NavContribution> Children)
{
    public NavContribution(
        string sectionKey, string key, Symbol icon, bool local, Func<Localizer, ObservableObject> open)
        : this(sectionKey, key, icon, local, open, [])
    {
    }
}