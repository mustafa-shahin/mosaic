using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.SignIn;

namespace Mosaic.Desktop.Tests.Shell.SignIn;

public sealed class SignInViewModelTests
{
    private static Localizer English() => Localizer.Load(LanguageCatalog.Resolve("en"));

    private static AuthApi ClientWith(HttpMessageHandler handler, string address = "http://localhost:5175/") =>
        new(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri(address) }, new CurrentSession()));

    private static SignInViewModel ViewModelWith(HttpMessageHandler handler) =>
        new(ClientWith(handler), English()) { Email = "ada@example.com", Password = "correct horse battery staple" };

    [Fact]
    public async Task SignIn_WhenTheServerAccepts_RaisesSignedInOnceWithTheAnswerAndNoRefusal()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, SignInBodies.Accepted));
        var signedIn = new List<SignInResult>();
        var refused = 0;

        viewModel.SignedIn += (_, answer) => signedIn.Add(answer);
        viewModel.Refused += (_, _) => refused++;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal("Ada Lovelace", Assert.Single(signedIn).Session.FullName);
        Assert.Equal(0, refused);
        Assert.False(viewModel.HasError);
    }

    [Theory]
    [InlineData(SignInNote.AccountReady, "sign_in.account_ready")]
    [InlineData(SignInNote.SessionEnded, "sign_in.session_ended")]
    [InlineData(SignInNote.SignedOutEverywhere, "sign_in.signed_out_everywhere")]
    public void Note_ShowsItsOwnSentence(SignInNote note, string key)
    {
        var viewModel = new SignInViewModel(ClientWith(new NeverRespondingHandler()), English()) { Note = note };

        Assert.True(viewModel.HasNote);
        Assert.Equal(viewModel.Strings.Translate(key), viewModel.NoteText);
    }

    [Fact]
    public void Note_WhenTheStartScreenOpenedSignIn_IsNone()
    {
        var viewModel = new SignInViewModel(ClientWith(new NeverRespondingHandler()), English());

        Assert.False(viewModel.HasNote);
        Assert.Null(viewModel.NoteText);
    }

    [Fact]
    public async Task SignIn_WhenThePasswordIsWrong_ShowsTheServerReasonInTheScreenLanguage()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.Forbidden, SignInBodies.WrongPassword));
        var signedIn = 0;
        var refused = 0;

        viewModel.SignedIn += (_, _) => signedIn++;
        viewModel.Refused += (_, _) => refused++;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(ErrorCodes.InvalidCredentials, viewModel.ErrorCode);
        Assert.True(viewModel.HasError);
        Assert.Equal(viewModel.Strings.Translate(ErrorCodes.InvalidCredentials), viewModel.ErrorMessage);
        Assert.Equal(0, signedIn);
        Assert.Equal(1, refused);
    }

    // the server owns both rules, so the screen must not trim, lower-case or shorten anything
    [Fact]
    public async Task SignIn_SendsTheAddressAndThePasswordExactlyAsTyped()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, SignInBodies.Accepted);
        var viewModel = new SignInViewModel(ClientWith(recorder), English())
        {
            Email = " Ada@Example.com ",
            Password = "  two spaces at each end  "
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal("""{"email":" Ada@Example.com ","password":"  two spaces at each end  "}""", recorder.Body);
    }

    [Fact]
    public async Task SignIn_WhenTheServerCannotBeReached_SaysSoInsteadOfBlamingThePassword()
    {
        var viewModel = ViewModelWith(new ThrowingHandler(new HttpRequestException("no route to host")));

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(DesktopErrorCodes.ApiUnreachable, viewModel.ErrorCode);
    }

    // an envelope that says success but carries no session is not a sign-in
    [Fact]
    public async Task SignIn_WhenASuccessCarriesNoData_ReportsUnexpectedResponse()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, "{}"));
        var signedIn = 0;

        viewModel.SignedIn += (_, _) => signedIn++;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(0, signedIn);
        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, viewModel.ErrorCode);
    }

    [Fact]
    public async Task SignIn_ClearsTheReasonOfTheLastRefusalFirst()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, SignInBodies.Accepted));

        viewModel.ErrorCode = ErrorCodes.TooShort;
        viewModel.ErrorParams = new Dictionary<string, string> { [ErrorParams.Limit] = "15" };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorCode);
        Assert.Null(viewModel.ErrorParams);
    }

    // Enter and the button both go through the command, so this is what stops a second request
    [Fact]
    public void SignIn_WhileARequestIsRunning_CannotStartASecond()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());

        viewModel.SignInCommand.Execute(null);

        Assert.True(viewModel.SignInCommand.IsRunning);
        Assert.False(viewModel.SignInCommand.CanExecute(null));
    }

    [Fact]
    public void Server_IsTheHostAndPortTheRequestGoesTo()
    {
        var viewModel = new SignInViewModel(
            ClientWith(new NeverRespondingHandler(), "https://core.acme.internal:8443/"), English());

        Assert.Equal("core.acme.internal:8443", viewModel.Server);
    }

    [Fact]
    public void Server_WhenNoAddressIsConfigured_IsEmpty()
    {
        var viewModel = new SignInViewModel(new AuthApi(new ApiClient(new HttpClient(new NeverRespondingHandler()), new CurrentSession())), English());

        Assert.Equal(string.Empty, viewModel.Server);
    }
}
