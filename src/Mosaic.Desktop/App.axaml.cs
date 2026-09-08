using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Shell;

namespace Mosaic.Desktop;

public partial class App : Application
{
    private readonly SettingsStore _settings = new();

    private ApiClient _api = null!;
    private MainWindow _window = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var stored = _settings.Read();

            // before the window exists, otherwise it flashes
            RequestedThemeVariant = Appearances.IsDark(stored.Appearance) ? ThemeVariant.Dark : ThemeVariant.Light;

            // one HttpClient for the whole app. Auth handlers will go here later.
            _api = new ApiClient(new HttpClient { BaseAddress = ApiEndpoint.FromEnvironment() });

            // stored preference, else the OS language. English if that isn't shipped.
            var language = LanguageCatalog.Resolve(stored.LanguageCode ?? CultureInfo.CurrentUICulture.Name);

            _window = new MainWindow { DataContext = BuildShell(language, startAt: null) };

            desktop.MainWindow = _window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Rebuilt on every language change. The window, ApiClient and SettingsStore live longer.
    private ShellViewModel BuildShell(LanguageOption language, string? startAt)
    {
        var strings = Localizer.Load(language);
        var settings = new SettingsViewModel(strings, _settings);

        settings.LanguageChosen += OnLanguageChosen;

        return new ShellViewModel(strings, new HealthViewModel(_api, strings), settings, startAt);
    }

    private void OnLanguageChosen(object? sender, LanguageOption language)
    {
        // unsubscribe, this instance is about to be thrown away
        if (sender is SettingsViewModel asked)
        {
            asked.LanguageChosen -= OnLanguageChosen;
        }

        // we're still inside the ComboBox binding update, don't tear the tree down synchronously
        Dispatcher.UIThread.Post(() =>
        {
            if (_window.DataContext is not ShellViewModel standing ||
                string.Equals(standing.Strings.Language.Code, language.Code, StringComparison.Ordinal))
            {
                return;
            }

            _window.DataContext = BuildShell(language, standing.ActiveKey);
        });
    }
}
