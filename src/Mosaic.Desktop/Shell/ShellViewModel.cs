using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Shell;

// Sidebar, header title and the current screen. Names no feature: the composition says what exists,
// the session says what this user may see, and NavBuilder does the work.
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly List<NavEntry> _entries;

    public ShellViewModel(
       Localizer strings, IDesktopComposition composition, IReadOnlySet<string> visible, string? startAt = null)
    {
        Strings = strings;
        Sections = NavBuilder.Build(composition, visible, strings);
        _entries = NavBuilder.Flatten(Sections);

        if (_entries.Count == 0)
        {
            Current = new NoAccessViewModel(strings);
            return;
        }

        var opening = _entries.Find(entry => entry.Key == startAt) ?? _entries[0];

        opening.IsActive = true;
        Current = opening.Open(strings);
    }

    public Localizer Strings { get; }

    public IReadOnlyList<NavSection> Sections { get; }

    // Passed back as startAt when the shell is rebuilt after a language change.
    public string ActiveKey => Active?.Key ?? string.Empty;

    [ObservableProperty]
    public partial ObservableObject Current { get; set; }

    public string Title => Active?.Label ?? string.Empty;

    private NavEntry? Active => _entries.Find(entry => entry.IsActive);

    // A language change throws this whole shell away, and the screen standing open would otherwise
    // keep running with nobody bound to it.
    public void Dispose() => (Current as IDisposable)?.Dispose();

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

        // a screen that polls or fetches stops here, which is what "only while it is open" means
        (Current as IDisposable)?.Dispose();

        Current = entry.Open(Strings);
        OnPropertyChanged(nameof(Title));
    }
}