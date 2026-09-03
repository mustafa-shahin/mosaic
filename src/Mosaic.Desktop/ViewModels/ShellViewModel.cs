using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.ViewModels;

// Sidebar, header title and the current page.
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly List<NavEntry> _entries;

    public ShellViewModel(
        Localizer strings, HealthViewModel health, SettingsViewModel settings, string? startAt = null)
    {
        Strings = strings;

        NavEntry Entry(string key, Symbol icon, ObservableObject target) =>
            new(key, strings.Translate(key), icon, target);

        var dashboard = Entry("nav.dashboard", Symbol.Board, health);
        var preferences = Entry("nav.settings", Symbol.Settings, settings);

        _entries = [dashboard, preferences];

        Sections =
        [
            new NavSection(strings.Translate("nav.overview"), [dashboard]),
            new NavSection(strings.Translate("nav.administration"), [preferences])
        ];

        var opening = _entries.Find(entry => entry.Key == startAt) ?? dashboard;

        opening.IsActive = true;
        _current = opening.Target;
    }

    public Localizer Strings { get; }

    public IReadOnlyList<NavSection> Sections { get; }

    // Passed back as startAt when the shell is rebuilt after a language change.
    public string ActiveKey => Active?.Key ?? string.Empty;

    [ObservableProperty]
    private ObservableObject _current;

    public string Title => Active?.Label ?? string.Empty;

    private NavEntry? Active => _entries.Find(entry => entry.IsActive);

    [RelayCommand]
    private void Go(NavEntry entry)
    {
        if (entry.IsActive)
        {
            return;
        }

        foreach (var candidate in _entries)
        {
            candidate.IsActive = ReferenceEquals(candidate, entry);
        }

        Current = entry.Target;
        OnPropertyChanged(nameof(Title));
    }
}
