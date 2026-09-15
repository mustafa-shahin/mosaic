using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

public partial class App : Application
{
    private readonly Func<DesktopServices, IDesktopComposition> _compose;
    private readonly SettingsStore _settings = new();

    private ApiClient _api = null!;
    private IDesktopComposition _composition = null!;
    private MainWindow _window = null!;

    // the previewer and a plain core build both compose the core edition
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

            // the pool caches the resolved address, so a connection has to retire before a DNS change lands
            var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

            // one HttpClient for the whole app. Auth handlers will go here later.
            _api = new ApiClient(new HttpClient(handler) { BaseAddress = ApiEndpoint.FromEnvironment() });

            // built once: keys, icons, access rules and factories, none of which depend on the language
            _composition = _compose(new DesktopServices(_api, _settings, OnLanguageChosen));

            foreach (var template in _composition.Templates)
            {
                DataTemplates.Add(template);
            }

            // stored preference, else the OS language. English if that isn't shipped.
            var language = LanguageCatalog.Resolve(stored.LanguageCode ?? CultureInfo.CurrentUICulture.Name);

            _window = new MainWindow { DataContext = BuildShell(language, startAt: null) };

            desktop.MainWindow = _window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Rebuilt on every language change. The window, the ApiClient and the composition live longer.
    // Nothing is served until there is a login, so only the local entries show.
    private ShellViewModel BuildShell(LanguageOption language, string? startAt)
        => new(Localizer.Load(language), _composition, new HashSet<string>(StringComparer.Ordinal), startAt);

    private void OnLanguageChosen(object? sender, LanguageOption language)
    {
        // we're still inside the ComboBox binding update, don't tear the tree down synchronously
        Dispatcher.UIThread.Post(() =>
        {
            // Only the screen standing open may ask. A request from one that has since been left or
            // rebuilt is stale, whatever feature it came from, so App never learns the sender's type.
            if (_window.DataContext is not ShellViewModel standing ||
                !ReferenceEquals(standing.Current, sender) ||
                string.Equals(standing.Strings.Language.Code, language.Code, StringComparison.Ordinal))
            {
                return;
            }

            // swap first, so nothing is ever bound to a shell that has been disposed
            _window.DataContext = BuildShell(language, standing.ActiveKey);

            standing.Dispose();
        });
    }
    
}