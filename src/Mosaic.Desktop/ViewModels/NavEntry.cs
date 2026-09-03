using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace Mosaic.Desktop.ViewModels;

public sealed partial class NavEntry(string key, string label, Symbol icon, ObservableObject target)
    : ObservableObject
{
    // Stable id (the translation key). Label changes with the language, Key doesn't.
    public string Key { get; } = key;

    public string Label { get; } = label;

    public Symbol Icon { get; } = icon;

    public ObservableObject Target { get; } = target;

    [ObservableProperty]
    private bool _isActive;
}

public sealed class NavSection(string caption, IReadOnlyList<NavEntry> entries)
{
    public string Caption { get; } = caption;

    public IReadOnlyList<NavEntry> Entries { get; } = entries;
}
