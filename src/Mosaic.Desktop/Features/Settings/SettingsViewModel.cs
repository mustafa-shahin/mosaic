using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string DarkKey = "settings.appearance_dark";
    private const string LightKey = "settings.appearance_light";

    private readonly SettingsStore _store;
    private readonly bool _constructed;

    public SettingsViewModel(Localizer strings, SettingsStore store)
    {
        Strings = strings;
        _store = store;

        IsDark = Appearances.IsDark(store.Read().Appearance);
        SelectedLanguage = strings.Language;

        _constructed = true;
    }

    // Handled in App, which rebuilds the whole shell including this instance.
    public event EventHandler<LanguageOption>? LanguageChosen;

    public Localizer Strings { get; }

    public IReadOnlyList<LanguageOption> Languages { get; } = LanguageCatalog.Available;

    // ComboBox can push null through the binding.
    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppearanceName))]
    [NotifyPropertyChangedFor(nameof(AppearanceAction))]
    public partial bool IsDark { get; set; }

    public string AppearanceName => Strings.Translate(IsDark ? DarkKey : LightKey);

    // tooltip on the toggle button: what clicking does, not the current state
    public string AppearanceAction =>
        Strings.Translate(IsDark ? "settings.switch_to_light" : "settings.switch_to_dark");

    // Generated setter already skips same-value assignments.
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        // the constructor assigns through the setter now, and starting up is not choosing
        if (!_constructed || value is null)
        {
            return;
        }

        _store.Write(_store.Read() with { LanguageCode = value.Code });

        LanguageChosen?.Invoke(this, value);
    }

    [RelayCommand]
    private void ToggleAppearance()
    {
        IsDark = !IsDark;

        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        _store.Write(_store.Read() with { Appearance = IsDark ? Appearances.Dark : Appearances.Light });
    }
}
