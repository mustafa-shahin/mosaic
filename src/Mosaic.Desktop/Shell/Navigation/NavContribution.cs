using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// A heading in the sidebar. The composition declares them in the order they appear.
public sealed record NavSectionDefinition(string Key, string CaptionKey);

// One screen a feature offers the sidebar. Key is both the translation key and the stable id that
// survives a language change. Local means the screen needs no server. Open runs when the user enters
// the screen and its result is dropped when they leave. Nothing here is language-bound, so a
// composition is built once per process.
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