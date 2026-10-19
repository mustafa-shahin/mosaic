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
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

public partial class App : Application
{
    private readonly Func<DesktopServices, IDesktopComposition> _compose;
    private readonly SettingsStore _settings = new();

    private MainWindowViewModel _main = null!;

    // for the headless tests, which create App from its type
    public App() : this(services => new CoreComposition(services))
    {
    }

    // pro hands in a composition that wraps core's, which is how core never names pro
    public App(Func<DesktopServices, IDesktopComposition> compose) => _compose = compose;

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

            // built once: keys, icons, access rules and factories, none of which depend on the language
            var composition = _compose(new DesktopServices(api, _settings, signOut, OnLanguageChosen));

            foreach (var template in composition.Templates)
            {
                DataTemplates.Add(template);
            }

            // stored preference, else the OS language. English if that isn't shipped.
            var language = LanguageCatalog.Resolve(stored.LanguageCode ?? CultureInfo.CurrentUICulture.Name);

            _main = new MainWindowViewModel(auth, session, signOut, composition, language);

            desktop.MainWindow = new MainWindow { DataContext = _main };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // still inside the ComboBox binding update here, so the tree must not be torn down synchronously
    private void OnLanguageChosen(object? sender, LanguageOption language) =>
        Dispatcher.UIThread.Post(() => _main.ChooseLanguage(sender, language));
}
