using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell;

namespace Mosaic.Desktop;

public partial class App : Application
{
    private readonly SettingsStore _settings = new();

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

            // a pooled connection stays on the address it connected to, so it has to retire before a DNS change lands
            var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

            // the token lives here from sign-in until sign-out, in memory only
            var session = new CurrentSession();

            // one HttpClient for the whole app
            var api = new ApiClient(new HttpClient(handler) { BaseAddress = ApiEndpoint.FromEnvironment() }, session);
            var auth = new AuthApi(api);
            var signOut = new SignOut(auth, session);

            // stored preference, else the OS language. English if that isn't shipped.
            var language = new LanguageChoice(
                _settings, LanguageCatalog.Resolve(stored.LanguageCode ?? CultureInfo.CurrentUICulture.Name));

            // built once: keys, icons and factories, none of which depend on the language
            var composition = CoreComposition.Build(new DesktopServices(api, _settings, signOut, language));

            var main = new MainWindowViewModel(auth, session, signOut, composition, language.Chosen);

            // the ComboBox is still inside its binding update when the choice arrives, so the shell is rebuilt after it
            language.Changed += (_, chosen) => Dispatcher.UIThread.Post(() => main.SwitchLanguage(chosen));

            desktop.MainWindow = new MainWindow { DataContext = main };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
