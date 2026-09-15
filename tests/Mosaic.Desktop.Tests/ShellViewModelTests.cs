using FluentIcons.Common;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests;

public sealed class ShellViewModelTests : IDisposable
{
    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>(StringComparer.Ordinal);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));

    private ShellViewModel Shell(string? startAt = null) => Shell(_strings, startAt);

    // the real composition, so a broken one fails here rather than only at startup
    private ShellViewModel Shell(Localizer strings, string? startAt = null)
    {
        var api = new ApiClient(new HttpClient(new ThrowingHandler(new InvalidOperationException("must not be called"))));
        var services = new DesktopServices(api, new SettingsStore(_folder), (_, _) => { });

        return new ShellViewModel(strings, new CoreComposition(services), Nothing, startAt);
    }

    private static NavEntry EntryFor(ShellViewModel shell, string key) =>
        shell.Sections.SelectMany(section => section.Entries).Single(entry => entry.Key == key);

    [Fact]
    public void Current_OnStart_IsTheDashboard()
    {
        var shell = Shell();

        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.Equal(_strings.Translate("nav.dashboard"), shell.Title);
        Assert.Equal("nav.dashboard", shell.ActiveKey);
    }

    [Fact]
    public void Go_ToAnotherEntry_ShowsItMovesTheMarkerAndRenamesTheHeader()
    {
        var shell = Shell();
        var settings = EntryFor(shell, "nav.settings");

        shell.GoCommand.Execute(settings);

        Assert.IsType<SettingsViewModel>(shell.Current);
        Assert.True(settings.IsActive);
        Assert.False(EntryFor(shell, "nav.dashboard").IsActive);
        Assert.Equal(_strings.Translate("nav.settings"), shell.Title);
        Assert.Equal("nav.settings", shell.ActiveKey);
    }

    [Fact]
    public void StartAt_APreviousScreen_OpensThereInsteadOfTheDashboard()
    {
        var rebuilt = Shell(startAt: "nav.settings");

        Assert.IsType<SettingsViewModel>(rebuilt.Current);
        Assert.Equal("nav.settings", rebuilt.ActiveKey);
        Assert.True(EntryFor(rebuilt, "nav.settings").IsActive);
    }

    [Fact]
    public void StartAt_AScreenThatNoLongerExists_FallsBackToTheDashboard()
    {
        var rebuilt = Shell(startAt: "nav.gone");

        Assert.IsType<HealthViewModel>(rebuilt.Current);
        Assert.Equal("nav.dashboard", rebuilt.ActiveKey);
    }

    [Fact]
    public void Labels_ComeFromTheLanguageTheShellWasBuiltWith()
    {
        var german = Localizer.Load(LanguageCatalog.Resolve("de"));

        var shell = Shell(german);

        Assert.Equal(german.Translate("nav.overview"), shell.Sections[0].Caption);
        Assert.Equal(german.Translate("nav.dashboard"), shell.Title);
        Assert.NotEqual(_strings.Translate("nav.overview"), shell.Sections[0].Caption);
    }

    [Fact]
    public void Go_AwayAndBack_BuildsAFreshScreen()
    {
        var shell = Shell();
        var first = shell.Current;

        shell.GoCommand.Execute(EntryFor(shell, "nav.settings"));
        shell.GoCommand.Execute(EntryFor(shell, "nav.dashboard"));

        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.NotSame(first, shell.Current);
    }

    [Fact]
    public void Go_DisposesTheScreenItLeaves()
    {
        DisposableScreen? opened = null;

        var composition = new FakeComposition(
            [new NavSectionDefinition("main", "nav.overview")],
            [
                new NavContribution("main", "one", Symbol.Board, local: true, _ =>
                {
                    opened = new DisposableScreen();

                    return opened;
                }),
                new NavContribution("main", "two", Symbol.Settings, local: true, _ => new FakeScreen())
            ]);

        var shell = new ShellViewModel(_strings, composition, Nothing);
        var left = opened;

        shell.GoCommand.Execute(shell.Sections[0].Entries[1]);

        Assert.NotNull(left);
        Assert.True(left.WasDisposed);
    }

    // What App does to the old shell after a language change.
    [Fact]
    public void Dispose_DisposesTheScreenThatIsStandingOpen()
    {
        DisposableScreen? opened = null;

        var composition = new FakeComposition(
            [new NavSectionDefinition("main", "nav.overview")],
            [
                new NavContribution("main", "one", Symbol.Board, local: true, _ =>
                {
                    opened = new DisposableScreen();

                    return opened;
                })
            ]);

        var shell = new ShellViewModel(_strings, composition, Nothing);

        shell.Dispose();

        Assert.NotNull(opened);
        Assert.True(opened.WasDisposed);
    }

    [Fact]
    public void Construct_WhenNothingIsVisible_ShowsNoAccessInsteadOfThrowing()
    {
        var composition = new FakeComposition(
            [new NavSectionDefinition("main", "nav.overview")],
            [new NavContribution("main", "one", Symbol.Board, local: false, _ => new FakeScreen())]);

        var shell = new ShellViewModel(_strings, composition, Nothing);

        Assert.IsType<NoAccessViewModel>(shell.Current);
        Assert.Empty(shell.Sections);
        Assert.Equal(string.Empty, shell.ActiveKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
