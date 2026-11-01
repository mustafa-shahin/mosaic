using System.Net;
using FluentIcons.Common;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.Navigation;
using Mosaic.Desktop.Tests.Shell.Navigation;

namespace Mosaic.Desktop.Tests.Shell;

public sealed class ShellViewModelTests : IDisposable
{
    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>(StringComparer.Ordinal);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));

    private readonly RecordingHandler _network = new(HttpStatusCode.OK, "{}");

    private readonly CurrentSession _session = new();

    private readonly SessionResult _person;

    public ShellViewModelTests()
    {
        _person = _session.Start(SignInResults.Ada()).Person;
    }

    private SignOut SignOutOver(HttpMessageHandler handler) =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, _session)), _session);

    private ShellViewModel Shell(string? startAt = null) => Shell(_strings, startAt);

    // the real composition, so a broken one fails here before a user meets it after signing in
    private ShellViewModel Shell(Localizer strings, string? startAt = null)
    {
        var api = new ApiClient(new HttpClient(_network) { BaseAddress = new Uri("http://localhost") }, _session);
        var services = new DesktopServices(api, new SettingsStore(_folder), SignOutOver(_network), (_, _) => { });

        return new ShellViewModel(strings, new CoreComposition(services), Nothing, _person, SignOutOver(_network), startAt);
    }

    private ShellViewModel ShellOf(IDesktopComposition composition) =>
        new(_strings, composition, Nothing, _person, SignOutOver(_network));

    private ShellViewModel ShellFor(string fullName) =>
        new(_strings, new FakeComposition([], []), Nothing, _person with { FullName = fullName }, SignOutOver(_network));

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

        var shell = ShellOf(composition);
        var left = opened;

        shell.GoCommand.Execute(shell.Sections[0].Entries[1]);

        Assert.NotNull(left);
        Assert.True(left.WasDisposed);
    }

    // What MainWindowViewModel does to the old shell after a language change.
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

        var shell = ShellOf(composition);

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

        var shell = ShellOf(composition);

        Assert.IsType<NoAccessViewModel>(shell.Current);
        Assert.Empty(shell.Sections);
        Assert.Equal(string.Empty, shell.ActiveKey);
    }

    [Fact]
    public void TheChip_ShowsThePersonFromTheSignInAnswer()
    {
        var shell = Shell();

        Assert.Equal("Ada Lovelace", shell.Name);
        Assert.Equal("ada@example.com", shell.Email);
        Assert.Equal("AL", shell.Initials);
    }

    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("ada lovelace", "AL")]
    [InlineData("Anna Maria Schmidt", "AS")]
    [InlineData("Plato", "P")]
    [InlineData("أحمد علي", "أع")]
    public void Initials_AreTheFirstLettersOfTheFirstAndTheLastName(string fullName, string initials)
    {
        Assert.Equal(initials, ShellFor(fullName).Initials);
    }

    // U+0301 is the acute accent written as a second character after its letter
    [Fact]
    public void Initials_KeepACombiningAccentOnItsLetter()
    {
        var accent = (char)0x0301;

        var initials = ShellFor($"e{accent}mile Zola").Initials;

        Assert.Equal($"E{accent}Z", initials);
    }

    [Fact]
    public void SignOutHere_EndsTheSessionAtOnceAndThenTellsTheServer()
    {
        var server = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var shell = new ShellViewModel(_strings, new FakeComposition([], []), Nothing, _person, SignOutOver(server));
        var ends = new List<SessionEnded>();

        _session.Ended += (_, ended) => ends.Add(ended);

        shell.SignOutHereCommand.Execute(null);

        Assert.Null(_session.Current);
        Assert.Equal(SessionEndReason.SignedOut, Assert.Single(ends).Reason);
        Assert.Equal(ApiPaths.Auth + AuthRoutes.SignOut, server.Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        // a screen calls the network only when its view asks, and no view exists here
        Assert.True(_network.Calls == 0, $"A screen sent {_network.Method} {_network.Path} before a view asked it to.");
    }
}
