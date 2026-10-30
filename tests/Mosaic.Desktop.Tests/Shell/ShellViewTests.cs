using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests.Shell;

public sealed class ShellViewTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly CurrentSession _session = new();

    // the dashboard checks the API as soon as its view appears, and a check that never answers keeps it quiet
    private ShellViewModel Shell(HttpMessageHandler signOutServer, string language = "en")
    {
        var api = new ApiClient(new HttpClient(new NeverRespondingHandler()) { BaseAddress = new Uri("http://localhost") }, _session);
        var signOut = new SignOut(
            new AuthApi(new ApiClient(new HttpClient(signOutServer) { BaseAddress = new Uri("http://localhost") }, _session)), _session);
        var composition = new CoreComposition(new DesktopServices(api, new SettingsStore(_folder), signOut, (_, _) => { }));
        var person = _session.Start(SignInResults.Ada()).Person;

        return new ShellViewModel(
            Localizer.Load(LanguageCatalog.Resolve(language)), composition, new HashSet<string>(StringComparer.Ordinal), person, signOut);
    }

    private static Window Shown(ShellViewModel shell)
    {
        var window = new Window { Width = 1360, Height = 848, FlowDirection = shell.Strings.FlowDirection, Content = new ShellView { DataContext = shell } };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // ShellViewModelTests prove the command swaps the screen. This proves the button reaches the command.
    [Fact]
    public void ClickingANavEntry_OpensItsScreen()
    {
        var shell = Shell(new NeverRespondingHandler());
        var window = Shown(shell);

        var settings = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.DataContext is NavEntry { Key: "nav.settings" });

        Click(window, settings);

        Assert.IsType<SettingsViewModel>(shell.Current);
    }

    [Fact]
    public void TheChip_ShowsTheInitialsTheNameAndTheEmail()
    {
        var window = Shown(Shell(new NeverRespondingHandler()));

        var chip = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AccountButton");
        var texts = chip.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.Equal(["AL", "Ada Lovelace", "ada@example.com"], texts);
    }

    [Theory]
    [InlineData("ar", TextAlignment.Right)]
    [InlineData("en", TextAlignment.Start)]
    public void TheChipsEmail_ReadsLeftToRightUnderTheName(string language, TextAlignment alignment)
    {
        var window = Shown(Shell(new NeverRespondingHandler(), language));

        var email = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("accountEmail"));

        Assert.Equal(FlowDirection.LeftToRight, email.FlowDirection);
        Assert.Equal(alignment, email.TextAlignment);
    }

    [Fact]
    public void TheChipsMenu_SignsOut()
    {
        var server = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var window = Shown(Shell(server));
        var chip = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AccountButton");

        Click(window, chip);

        var signOut = Assert.Single(Assert.IsType<MenuFlyout>(chip.Flyout).Items.OfType<MenuItem>());

        Assert.Equal("Sign out", signOut.Header);

        signOut.Command!.Execute(signOut.CommandParameter);

        Assert.Null(_session.Current);
        Assert.Equal(ApiPaths.Auth + AuthRoutes.SignOut, server.Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
