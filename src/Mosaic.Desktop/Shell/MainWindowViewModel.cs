using CommunityToolkit.Mvvm.ComponentModel;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.FirstRun;
using Mosaic.Desktop.Shell.Navigation;
using Mosaic.Desktop.Shell.SignIn;
using Mosaic.Desktop.Shell.Start;

namespace Mosaic.Desktop.Shell;

// The start screen, then first run or sign-in, then the shell. It outlives them all, so the language lives here.
public sealed partial class MainWindowViewModel : ObservableObject
{
    // the server lists no entries yet, so only the local ones show
    private static readonly IReadOnlySet<string> NoServedEntries = new HashSet<string>(StringComparer.Ordinal);

    private readonly AuthApi _auth;
    private readonly CurrentSession _session;
    private readonly SignOut _signOut;
    private readonly IDesktopComposition _composition;

    public MainWindowViewModel(
        AuthApi auth, CurrentSession session, SignOut signOut, IDesktopComposition composition, LanguageOption language)
    {
        _auth = auth;
        _session = session;
        _signOut = signOut;
        _composition = composition;

        Strings = Localizer.Load(language);
        Current = Start();

        session.Ended += OnSessionEnded;
    }

    [ObservableProperty]
    public partial Localizer Strings { get; private set; }

    [ObservableProperty]
    public partial ObservableObject Current { get; private set; }

    public void ChooseLanguage(object? sender, LanguageOption language)
    {
        // only the screen standing open may ask, a request from one already left or rebuilt is stale
        if (Current is not ShellViewModel standing ||
            !ReferenceEquals(standing.Current, sender) ||
            string.Equals(Strings.Language.Code, language.Code, StringComparison.Ordinal))
        {
            return;
        }

        Strings = Localizer.Load(language);

        // swap first, so nothing is ever bound to a shell that has been disposed
        Current = Shell(standing.ActiveKey);

        standing.Dispose();
    }

    private StartViewModel Start()
    {
        var start = new StartViewModel(_auth, Strings);

        start.Answered += (_, rules) => Current = rules.Registration == RegistrationState.Unclaimed
            ? FirstRun(rules.Password)
            : SignIn(handover: null);

        return start;
    }

    private FirstRunViewModel FirstRun(PasswordRule rule)
    {
        var firstRun = new FirstRunViewModel(_auth, Strings, rule);

        firstRun.SignedIn += (_, signedIn) => OpenShell(signedIn);
        firstRun.HandedOver += (_, handover) => Current = SignIn(handover);

        return firstRun;
    }

    private SignInViewModel SignIn(SignInHandover? handover)
    {
        var signIn = handover is null
            ? new SignInViewModel(_auth, Strings)
            : new SignInViewModel(_auth, Strings)
            {
                Email = handover.Email,
                Note = handover.Note,
                ErrorCode = handover.ErrorCode,
                ErrorParams = handover.ErrorParams,
            };

        signIn.SignedIn += (_, signedIn) => OpenShell(signedIn);

        return signIn;
    }

    private void OpenShell(SignInResult signedIn)
    {
        _session.Start(signedIn);

        Current = Shell();
    }

    // every screen in the shell belonged to the session that ended, so the whole shell goes
    private void OnSessionEnded(object? sender, SessionEnded ended)
    {
        var shell = Current as ShellViewModel;

        Current = SignIn(new SignInHandover(ended.Session.Person.Email, NoteFor(ended.Reason)));

        shell?.Dispose();
    }

    private static SignInNote? NoteFor(SessionEndReason reason) => reason switch
    {
        SessionEndReason.Refused => SignInNote.SessionEnded,
        SessionEndReason.SignedOutEverywhere => SignInNote.SignedOutEverywhere,
        _ => null
    };

    private ShellViewModel Shell(string? startAt = null)
    {
        // the shell exists only while a session does, and it shows who holds it
        var person = _session.Current?.Person
            ?? throw new InvalidOperationException("The shell was asked for without a session.");

        return new ShellViewModel(Strings, _composition, NoServedEntries, person, _signOut, startAt);
    }
}
