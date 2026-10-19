using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// What the sidebar binds to, built by NavBuilder with the label already translated.
public sealed partial class NavEntry(NavContribution contribution, string label, IReadOnlyList<NavEntry> children)
    : ObservableObject
{
    // the translation key, stable across a language change
    public string Key => contribution.Key;

    public string Label { get; } = label;

    public Symbol Icon => contribution.Icon;

    public IReadOnlyList<NavEntry> Children { get; } = children;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public ObservableObject Open(Localizer strings) => contribution.Open(strings);
}

public sealed class NavSection(string caption, IReadOnlyList<NavEntry> entries)
{
    public string Caption { get; } = caption;

    public IReadOnlyList<NavEntry> Entries { get; } = entries;
}