using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Shell;

// Sidebar, header title and the current screen. Names no feature: its entries come from the composition.
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly List<NavEntry> _entries;
    private readonly SignOut _signOut;

    public ShellViewModel(
        Localizer strings, DesktopComposition composition, SessionResult person, SignOut signOut, string? startAt = null)
    {
        Strings = strings;
        Sections = NavBuilder.Build(composition, strings);
        _entries = [.. Sections.SelectMany(section => section.Entries)];
        _signOut = signOut;

        Name = person.FullName;
        Email = person.Email;
        Initials = NameInitials.Of(person.FullName);

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

    // the person signed in, for the account chip at the foot of the sidebar
    public string Name { get; }

    public string Email { get; }

    public string Initials { get; }

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

        // a screen that polls or fetches stops when it is left
        (Current as IDisposable)?.Dispose();

        Current = entry.Open(Strings);
        OnPropertyChanged(nameof(Title));
    }

    // the window leaves this shell at once, and the server hears about it afterwards
    [RelayCommand]
    private void SignOutHere() => _ = _signOut.HereAsync();
}
