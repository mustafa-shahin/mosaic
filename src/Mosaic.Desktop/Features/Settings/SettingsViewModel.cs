using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string DarkKey = "settings.appearance_dark";
    private const string LightKey = "settings.appearance_light";

    private readonly SettingsStore _store;
    private readonly SignOut _signOut;
    private readonly bool _constructed;

    public SettingsViewModel(Localizer strings, SettingsStore store, SignOut signOut)
    {
        Strings = strings;
        _store = store;
        _signOut = signOut;

        IsDark = Appearances.IsDark(store.Read().Appearance);
        SelectedLanguage = strings.Language;

        _constructed = true;
    }

    // App passes it on to MainWindowViewModel, which rebuilds the whole shell including this instance.
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

    // why the server did not sign everyone out, shown in the Sessions card
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial string? ErrorCode { get; set; }

    // placeholder values from the server, see Localizer.TranslateCode
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial IReadOnlyDictionary<string, string>? ErrorParams { get; set; }

    public bool HasError => ErrorCode is not null;

    public string? ErrorMessage => ErrorCode is null ? null : Strings.TranslateCode(ErrorCode, ErrorParams);

    // Generated setter already skips same-value assignments.
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        // also reached from the constructor, where saving would turn the OS language into a stored choice
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

    // on success the session has ended and the window has already left this screen, so only a refusal stays here
    [RelayCommand]
    private async Task SignOutEverywhereAsync(CancellationToken cancellationToken)
    {
        ErrorCode = null;
        ErrorParams = null;

        var answer = await _signOut.EverywhereAsync(cancellationToken);

        if (!answer.Success)
        {
            ErrorCode = answer.Error?.Code ?? DesktopErrorCodes.UnexpectedResponse;
            ErrorParams = answer.Error?.Params;
        }
    }
}
