using System.Net;
using FluentIcons.Common;
using Mosaic.Contracts;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.FirstRun;
using Mosaic.Desktop.Shell.Navigation;
using Mosaic.Desktop.Shell.SignIn;
using Mosaic.Desktop.Shell.Start;
using Mosaic.Desktop.Tests.Shell.Navigation;

namespace Mosaic.Desktop.Tests.Shell;

public sealed class MainWindowViewModelTests : IDisposable
{
    private const string Rules = "/v1/auth/rules";
    private const string Register = "/v1/auth/register";
    private const string SignInPath = "/v1/auth/sign-in";
    private const string SignOutPath = "/v1/auth/sign-out";

    private static readonly LanguageOption English = LanguageCatalog.Resolve("en");
    private static readonly LanguageOption German = LanguageCatalog.Resolve("de");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly RecordingHandler _network = new(HttpStatusCode.OK, "{}");

    // one session for the window and its composition, as App builds them
    private readonly CurrentSession _session = new();

    private RoutingHandler? _server;

    private AuthApi Answering(params (string Path, HttpStatusCode Status, string Body)[] answers)
    {
        _server = new RoutingHandler(answers.ToDictionary(answer => answer.Path, answer => (answer.Status, answer.Body)));

        return new AuthApi(new ApiClient(new HttpClient(_server) { BaseAddress = new Uri("http://localhost") }, _session));
    }

    // an instance that already has its owner, so the start screen leads to sign-in
    private AuthApi SetUp(HttpStatusCode signInStatus, string signInBody) =>
        Answering((Rules, HttpStatusCode.OK, RulesBodies.Closed), (SignInPath, signInStatus, signInBody));

    private AuthApi SignedInAndOut(HttpStatusCode signOutStatus) =>
        Answering(
            (Rules, HttpStatusCode.OK, RulesBodies.Closed),
            (SignInPath, HttpStatusCode.OK, SignInBodies.Accepted),
            (SignOutPath, signOutStatus, """{"success":true}"""));

    private AuthApi Unclaimed(HttpStatusCode registerStatus, string registerBody, HttpStatusCode signInStatus, string signInBody) =>
        Answering(
            (Rules, HttpStatusCode.OK, RulesBodies.Unclaimed),
            (Register, registerStatus, registerBody),
            (SignInPath, signInStatus, signInBody));

    private MainWindowViewModel Main(AuthApi auth, IDesktopComposition? composition = null, LanguageOption? language = null) =>
        new(auth, _session, new SignOut(auth, _session), composition ?? Composition(), language ?? English);

    // the real composition, whose screens never call the network unless a view asks them to
    private CoreComposition Composition()
    {
        var api = new ApiClient(new HttpClient(_network) { BaseAddress = new Uri("http://localhost") }, _session);

        return new CoreComposition(new DesktopServices(api, new SettingsStore(_folder), new SignOut(new AuthApi(api), _session), (_, _) => { }));
    }

    private static async Task Start(MainWindowViewModel main) =>
        await Assert.IsType<StartViewModel>(main.Current).CheckCommand.ExecuteAsync(null);

    private static async Task SignIn(MainWindowViewModel main)
    {
        await Start(main);
        await Assert.IsType<SignInViewModel>(main.Current).SignInCommand.ExecuteAsync(null);
    }

    private static async Task CreateTheOwner(MainWindowViewModel main)
    {
        await Start(main);

        var firstRun = Assert.IsType<FirstRunViewModel>(main.Current);

        firstRun.Email.Value = "ada@example.com";
        firstRun.Password.Value = "correct horse battery staple";
        firstRun.RepeatPassword.Value = "correct horse battery staple";

        await firstRun.CreateCommand.ExecuteAsync(null);
    }

    private static FakeComposition OneScreen(Action<DisposableScreen> opened) => new(
        [new NavSectionDefinition("main", "nav.overview")],
        [
            new NavContribution("main", "one", Symbol.Board, local: true, _ =>
            {
                var screen = new DisposableScreen();

                opened(screen);

                return screen;
            })
        ]);

    [Fact]
    public void Current_OnStart_IsTheStartScreenInTheChosenLanguage()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted), language: German);

        var start = Assert.IsType<StartViewModel>(main.Current);

        Assert.Equal("de", main.Strings.Language.Code);
        Assert.Same(main.Strings, start.Strings);
    }

    [Fact]
    public async Task Start_WhenTheInstanceHasAnOwner_OpensTheSignInScreen()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await Start(main);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        Assert.Same(main.Strings, signIn.Strings);
        Assert.Null(signIn.Note);
    }

    [Fact]
    public async Task Start_WhenTheInstanceIsUnclaimed_OpensFirstRunWithTheServersRule()
    {
        var main = Main(Answering((Rules, HttpStatusCode.OK, RulesBodies.Unclaimed)));

        await Start(main);

        var firstRun = Assert.IsType<FirstRunViewModel>(main.Current);

        Assert.Same(main.Strings, firstRun.Strings);
        Assert.StartsWith("At least 15 characters.", firstRun.PasswordGuidance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_WhenTheCheckFails_StaysOnTheStartScreen()
    {
        var unreachable = new AuthApi(new ApiClient(
            new HttpClient(new ThrowingHandler(new HttpRequestException("refused"))) { BaseAddress = new Uri("http://localhost") }, _session));
        var main = Main(unreachable);
        var before = main.Current;

        await Start(main);

        Assert.Same(before, main.Current);
    }

    [Fact]
    public async Task SignIn_WhenTheServerAccepts_StartsTheSessionAndOpensTheShellOnTheDashboard()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        var shell = Assert.IsType<ShellViewModel>(main.Current);

        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.Equal("nav.dashboard", shell.ActiveKey);
        Assert.Equal("mosaic_Zm9yLXRlc3RzLW9ubHktbm90LWEtcmVhbC10b2tlbi0xMjM0", _session.Current?.Token);
        Assert.Equal("Ada Lovelace", shell.Name);
        Assert.Equal("ada@example.com", shell.Email);
    }

    [Fact]
    public async Task SignIn_WhenRefused_StaysOnTheSignInScreenWithoutASession()
    {
        var main = Main(SetUp(HttpStatusCode.Forbidden, SignInBodies.WrongPassword));

        await Start(main);

        var before = main.Current;

        await Assert.IsType<SignInViewModel>(main.Current).SignInCommand.ExecuteAsync(null);

        Assert.Same(before, main.Current);
        Assert.Null(_session.Current);
    }

    [Fact]
    public async Task FirstRun_WhenTheOwnerIsCreatedAndSignedIn_StartsTheSessionAndOpensTheShell()
    {
        var main = Main(Unclaimed(HttpStatusCode.Created, RegisterBodies.Owner, HttpStatusCode.OK, SignInBodies.Accepted));

        await CreateTheOwner(main);

        Assert.Equal("nav.dashboard", Assert.IsType<ShellViewModel>(main.Current).ActiveKey);
        Assert.NotNull(_session.Current);
    }

    [Fact]
    public async Task FirstRun_WhenTheSignInAfterwardsFails_OpensSignInWithTheAddressTheNoteAndTheReason()
    {
        var main = Main(Unclaimed(
            HttpStatusCode.Created, RegisterBodies.Owner, HttpStatusCode.ServiceUnavailable, """{"error":{"code":"error.server_busy"}}"""));

        await CreateTheOwner(main);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        Assert.Equal("ada@example.com", signIn.Email);
        Assert.Equal(SignInNote.AccountReady, signIn.Note);
        Assert.Equal(ErrorCodes.ServerBusy, signIn.ErrorCode);
    }

    [Fact]
    public async Task FirstRun_WhenTheInstanceWasClaimedMeanwhile_OpensSignInWithTheAddressAndTheReason()
    {
        var main = Main(Unclaimed(HttpStatusCode.Forbidden, RegisterBodies.RegistrationClosed, HttpStatusCode.OK, SignInBodies.Accepted));

        await CreateTheOwner(main);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        Assert.Equal("ada@example.com", signIn.Email);
        Assert.Null(signIn.Note);
        Assert.Equal(ErrorCodes.RegistrationClosed, signIn.ErrorCode);
    }

    [Fact]
    public async Task SignOut_FromTheChip_OpensSignInWithTheAddressAndNoNote()
    {
        var main = Main(SignedInAndOut(HttpStatusCode.OK));

        await SignIn(main);

        Assert.IsType<ShellViewModel>(main.Current).SignOutHereCommand.Execute(null);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        Assert.Equal("ada@example.com", signIn.Email);
        Assert.Null(signIn.Note);
        Assert.Null(signIn.ErrorCode);
        Assert.Null(_session.Current);
    }

    // a session the server already deleted answers 401 to its own sign-out, and that must not bring the note for an ended session
    [Fact]
    public async Task SignOut_WhenTheServerAnswers401_StillShowsNoNote()
    {
        var main = Main(SignedInAndOut(HttpStatusCode.Unauthorized));

        await SignIn(main);

        Assert.IsType<ShellViewModel>(main.Current).SignOutHereCommand.Execute(null);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        await Task.Yield();

        Assert.Same(signIn, main.Current);
        Assert.Null(signIn.Note);
        Assert.Equal([Rules, SignInPath, SignOutPath], _server!.Paths);
    }

    [Theory]
    [InlineData(SessionEndReason.Refused, SignInNote.SessionEnded)]
    [InlineData(SessionEndReason.SignedOutEverywhere, SignInNote.SignedOutEverywhere)]
    public async Task SessionEnded_OpensSignInWithTheAddressAndItsNote(SessionEndReason reason, SignInNote note)
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        _session.End(_session.Current!, reason);

        var signIn = Assert.IsType<SignInViewModel>(main.Current);

        Assert.Equal("ada@example.com", signIn.Email);
        Assert.Equal(note, signIn.Note);
    }

    [Fact]
    public async Task SessionEnded_DisposesTheShellAndTheScreenStandingOpen()
    {
        DisposableScreen? opened = null;
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted), OneScreen(screen => opened = screen));

        await SignIn(main);

        _session.End(_session.Current!, SessionEndReason.Refused);

        Assert.NotNull(opened);
        Assert.True(opened.WasDisposed);
    }

    [Fact]
    public async Task ChooseLanguage_FromTheScreenStandingOpen_RebuildsTheShellThereInThatLanguage()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        var standing = Assert.IsType<ShellViewModel>(main.Current);

        standing.GoCommand.Execute(standing.Sections.SelectMany(section => section.Entries).Single(entry => entry.Key == "nav.settings"));

        main.ChooseLanguage(standing.Current, German);

        var rebuilt = Assert.IsType<ShellViewModel>(main.Current);

        Assert.NotSame(standing, rebuilt);
        Assert.Equal("de", main.Strings.Language.Code);
        Assert.Same(main.Strings, rebuilt.Strings);
        Assert.Equal("nav.settings", rebuilt.ActiveKey);
    }

    [Fact]
    public async Task ChooseLanguage_KeepsTheSessionAndThePerson()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        var session = _session.Current;
        var standing = Assert.IsType<ShellViewModel>(main.Current);

        main.ChooseLanguage(standing.Current, German);

        var rebuilt = Assert.IsType<ShellViewModel>(main.Current);

        Assert.Same(session, _session.Current);
        Assert.Equal("Ada Lovelace", rebuilt.Name);
        Assert.Equal("ada@example.com", rebuilt.Email);
    }

    [Fact]
    public async Task ChooseLanguage_DisposesTheShellItReplaces()
    {
        DisposableScreen? opened = null;
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted), OneScreen(screen => opened = screen));

        await SignIn(main);

        var first = opened;

        main.ChooseLanguage(first, German);

        Assert.NotNull(first);
        Assert.True(first.WasDisposed);
        Assert.NotSame(first, opened);
    }

    [Fact]
    public async Task ChooseLanguage_FromAScreenNoLongerOpen_ChangesNothing()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        var standing = main.Current;

        main.ChooseLanguage(new FakeScreen(), German);

        Assert.Same(standing, main.Current);
        Assert.Equal("en", main.Strings.Language.Code);
    }

    [Fact]
    public async Task ChooseLanguage_TheLanguageAlreadyShown_ChangesNothing()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));

        await SignIn(main);

        var standing = Assert.IsType<ShellViewModel>(main.Current);

        main.ChooseLanguage(standing.Current, English);

        Assert.Same(standing, main.Current);
    }

    // there is no language picker before sign-in, so a request then can only be stale
    [Fact]
    public void ChooseLanguage_BeforeSignIn_ChangesNothing()
    {
        var main = Main(SetUp(HttpStatusCode.OK, SignInBodies.Accepted));
        var start = main.Current;

        main.ChooseLanguage(start, German);

        Assert.Same(start, main.Current);
        Assert.Equal("en", main.Strings.Language.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        // after every test, because any of them may have opened a screen
        Assert.True(_network.Calls == 0, $"A screen sent {_network.Method} {_network.Path} before a view asked it to.");
    }
}
