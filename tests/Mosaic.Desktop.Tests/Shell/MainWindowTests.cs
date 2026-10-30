using System.Net;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.FirstRun;
using Mosaic.Desktop.Shell.SignIn;
using Mosaic.Desktop.Shell.Start;

namespace Mosaic.Desktop.Tests.Shell;

// a screen with no template still renders, as its ToString() in a text block, so only a window notices
public sealed class MainWindowTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private MainWindowViewModel Main(HttpMessageHandler handler)
    {
        var session = new CurrentSession();

        // the dashboard checks the API as soon as its view appears, and a check that never answers keeps it quiet
        var shellApi = new ApiClient(new HttpClient(new NeverRespondingHandler()) { BaseAddress = new Uri("http://localhost") }, session);

        var auth = new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, session));
        var signOut = new SignOut(auth, session);
        var composition = new CoreComposition(new DesktopServices(shellApi, new SettingsStore(_folder), signOut, (_, _) => { }));

        return new MainWindowViewModel(auth, session, signOut, composition, LanguageCatalog.Resolve("en"));
    }

    private static MainWindow Shown(MainWindowViewModel main)
    {
        var window = new MainWindow { DataContext = main };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    // the start screen's view starts its own check, so the next screen is there once that check has ended
    private static async Task<MainWindow> ShownPastTheStart(MainWindowViewModel main)
    {
        var start = Assert.IsType<StartViewModel>(main.Current);
        var window = Shown(main);

        await start.CheckCommand.ExecutionTask!;
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [Fact]
    public void WhileTheServerIsAsked_TheWindowShowsTheStartScreen()
    {
        var window = Shown(Main(new NeverRespondingHandler()));

        Assert.Single(window.GetVisualDescendants().OfType<StartView>());
    }

    [Fact]
    public async Task AnUnclaimedInstance_ShowsFirstRun()
    {
        var window = await ShownPastTheStart(Main(new CannedResponseHandler(HttpStatusCode.OK, RulesBodies.Unclaimed)));

        Assert.Empty(window.GetVisualDescendants().OfType<StartView>());
        Assert.Single(window.GetVisualDescendants().OfType<FirstRunView>());
    }

    [Fact]
    public async Task AnInstanceWithAnOwner_ShowsSignInAndThenTheShell()
    {
        var main = Main(new RoutingHandler(new Dictionary<string, (HttpStatusCode, string)>
        {
            [ApiPaths.Auth + AuthRoutes.Rules] = (HttpStatusCode.OK, RulesBodies.Closed),
            [ApiPaths.Auth + AuthRoutes.SignIn] = (HttpStatusCode.OK, SignInBodies.Accepted),
        }));
        var window = await ShownPastTheStart(main);

        Assert.Single(window.GetVisualDescendants().OfType<SignInView>());

        await Assert.IsType<SignInViewModel>(main.Current).SignInCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.GetVisualDescendants().OfType<SignInView>());
        Assert.Single(window.GetVisualDescendants().OfType<ShellView>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
