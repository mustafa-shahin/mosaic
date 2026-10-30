using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.FirstRun;
using Mosaic.Desktop.Shell.SignIn;

namespace Mosaic.Desktop.Tests.Shell.FirstRun;

public sealed class FirstRunViewModelTests
{
    private const string Register = ApiPaths.Auth + AuthRoutes.Register;
    private const string SignIn = ApiPaths.Auth + AuthRoutes.SignIn;
    private const string Password = "correct horse battery staple";

    private static readonly PasswordRule Rule = new() { MinLength = 15, MaxLength = 128 };

    private static Localizer English() => Localizer.Load(LanguageCatalog.Resolve("en"));

    private static RoutingHandler Answering(
        HttpStatusCode registerStatus, string registerBody,
        HttpStatusCode signInStatus = HttpStatusCode.OK, string signInBody = SignInBodies.Accepted) =>
        new(new Dictionary<string, (HttpStatusCode, string)>
        {
            [Register] = (registerStatus, registerBody),
            [SignIn] = (signInStatus, signInBody),
        });

    private static FirstRunViewModel Filled(HttpMessageHandler handler, PasswordRule? rule = null)
    {
        var viewModel = new FirstRunViewModel(
            new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            English(),
            rule ?? Rule);

        viewModel.SetupToken.Value = "the-token-from-the-file";
        viewModel.Email.Value = "ada@example.com";
        viewModel.FirstName.Value = "Ada";
        viewModel.LastName.Value = "Lovelace";
        viewModel.Password.Value = Password;
        viewModel.RepeatPassword.Value = Password;

        return viewModel;
    }

    [Fact]
    public async Task Create_WhenTheOwnerIsCreated_SignsInWithWhatWasTypedAndRaisesSignedInOnceWithTheAnswer()
    {
        var handler = Answering(HttpStatusCode.Created, RegisterBodies.Owner);
        var viewModel = Filled(handler);
        var signedIn = new List<SignInResult>();
        var handedOver = 0;

        viewModel.SignedIn += (_, answer) => signedIn.Add(answer);
        viewModel.HandedOver += (_, _) => handedOver++;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal([Register, SignIn], handler.Paths);
        Assert.Equal("""{"email":"ada@example.com","password":"correct horse battery staple"}""", handler.Bodies[1]);
        Assert.Equal("ada@example.com", Assert.Single(signedIn).Session.Email);
        Assert.Equal(0, handedOver);
    }

    // the server owns the rules, so the screen must not trim, lower-case or shorten anything
    [Fact]
    public async Task Create_SendsTheFormExactlyAsTyped()
    {
        var handler = Answering(HttpStatusCode.Created, RegisterBodies.Owner);
        var viewModel = Filled(handler);

        viewModel.Email.Value = " Ada@Example.com ";
        viewModel.Password.Value = "  two spaces at each end  ";
        viewModel.RepeatPassword.Value = "  two spaces at each end  ";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(
            """{"email":" Ada@Example.com ","firstName":"Ada","lastName":"Lovelace","password":"  two spaces at each end  ","setupToken":"the-token-from-the-file"}""",
            handler.Bodies[0]);
    }

    [Fact]
    public async Task Create_WhenTheTwoPasswordsDiffer_SendsNothingAndSaysSoUnderTheRepeat()
    {
        var handler = Answering(HttpStatusCode.Created, RegisterBodies.Owner);
        var viewModel = Filled(handler);
        var refused = 0;

        viewModel.Refused += (_, _) => refused++;
        viewModel.RepeatPassword.Value = "correct horse battery stapler";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Empty(handler.Paths);
        Assert.Equal(viewModel.Strings.Translate(DesktopErrorCodes.PasswordsDiffer), viewModel.RepeatPassword.Error);
        Assert.Equal(1, refused);
    }

    // the hash compares every character, so a difference in case is a different password
    [Fact]
    public async Task Create_WhenThePasswordsDifferOnlyInCase_StillSaysSo()
    {
        var handler = Answering(HttpStatusCode.Created, RegisterBodies.Owner);
        var viewModel = Filled(handler);

        viewModel.RepeatPassword.Value = "Correct horse battery staple";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Empty(handler.Paths);
        Assert.NotNull(viewModel.RepeatPassword.Error);
    }

    [Theory]
    [InlineData(RegisterBodies.SetupTokenRequired, ErrorCodes.SetupTokenRequired)]
    [InlineData(RegisterBodies.SetupTokenInvalid, ErrorCodes.SetupTokenInvalid)]
    public async Task Create_WhenTheTokenIsRefused_ShowsTheReasonUnderTheTokenField(string body, string code)
    {
        var handler = Answering(HttpStatusCode.Forbidden, body);
        var viewModel = Filled(handler);

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.Strings.Translate(code), viewModel.SetupToken.Error);
        Assert.False(viewModel.HasError);
        Assert.Equal([Register], handler.Paths);
    }

    [Fact]
    public async Task Create_WhenTheRulesRefuseFields_ShowsEachReasonUnderItsOwnField()
    {
        var viewModel = Filled(Answering(HttpStatusCode.BadRequest, RegisterBodies.InvalidEmailAndTooShort));
        var refused = 0;

        viewModel.Refused += (_, _) => refused++;
        viewModel.Email.Value = "ada.example.com";
        viewModel.Password.Value = "short";
        viewModel.RepeatPassword.Value = "short";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.Strings.Translate(ErrorCodes.InvalidEmail), viewModel.Email.Error);
        Assert.Equal("Use at least 15 characters.", viewModel.Password.Error);
        Assert.Null(viewModel.FirstName.Error);
        Assert.False(viewModel.HasError);
        Assert.Equal(1, refused);
    }

    [Fact]
    public async Task Create_WhenTheInstanceWasClaimedMeanwhile_HandsOverToSignInWithTheReason()
    {
        var handler = Answering(HttpStatusCode.Forbidden, RegisterBodies.RegistrationClosed);
        var viewModel = Filled(handler);
        SignInHandover? handover = null;

        viewModel.HandedOver += (_, given) => handover = given;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal([Register], handler.Paths);
        Assert.Equal(new SignInHandover("ada@example.com", Note: null, ErrorCodes.RegistrationClosed, null), handover);
        Assert.False(viewModel.HasError);
    }

    // a field this screen does not show must still leave the person a reason, or the button would seem to do nothing
    [Fact]
    public async Task Create_WhenTheServerNamesAFieldThisScreenLacks_StillSaysSomethingIsWrong()
    {
        var viewModel = Filled(Answering(HttpStatusCode.BadRequest, RegisterBodies.UnknownField));

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(ErrorCodes.ValidationFailed, viewModel.ErrorCode);
    }

    [Fact]
    public async Task Create_WhenTheServerCannotBeReached_SaysSoAboveTheForm()
    {
        var viewModel = Filled(new ThrowingHandler(new HttpRequestException("refused")));

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(DesktopErrorCodes.ApiUnreachable, viewModel.ErrorCode);
    }

    [Fact]
    public async Task Create_WhenTheSignInAfterwardsFails_HandsOverTheAddressAndTheReason()
    {
        var viewModel = Filled(Answering(
            HttpStatusCode.Created, RegisterBodies.Owner,
            HttpStatusCode.ServiceUnavailable, """{"error":{"code":"error.server_busy"}}"""));
        var signedIn = 0;
        SignInHandover? handover = null;

        viewModel.SignedIn += (_, _) => signedIn++;
        viewModel.HandedOver += (_, given) => handover = given;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(0, signedIn);
        Assert.Equal(new SignInHandover("ada@example.com", SignInNote.AccountReady, ErrorCodes.ServerBusy, null), handover);
    }

    [Fact]
    public async Task Create_ClearsTheReasonsOfTheLastAttemptFirst()
    {
        var viewModel = Filled(Answering(HttpStatusCode.Created, RegisterBodies.Owner));

        viewModel.ErrorCode = DesktopErrorCodes.ApiUnreachable;
        viewModel.SetupToken.Error = "old";
        viewModel.Password.Error = "old";
        viewModel.RepeatPassword.Error = "old";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorCode);
        Assert.Null(viewModel.SetupToken.Error);
        Assert.Null(viewModel.Password.Error);
        Assert.Null(viewModel.RepeatPassword.Error);
    }

    // Enter and the button both go through the command, so this is what stops a second registration
    [Fact]
    public void Create_WhileARequestIsRunning_CannotStartASecond()
    {
        var viewModel = Filled(new NeverRespondingHandler());

        viewModel.CreateCommand.Execute(null);

        Assert.True(viewModel.CreateCommand.IsRunning);
        Assert.False(viewModel.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void PasswordGuidance_NamesTheServersMinimum()
    {
        var viewModel = Filled(new NeverRespondingHandler(), new PasswordRule { MinLength = 20, MaxLength = 128 });

        Assert.StartsWith("At least 20 characters.", viewModel.PasswordGuidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_IsTheHostAndPortTheRequestGoesTo()
    {
        Assert.Equal("localhost:5175", Filled(new NeverRespondingHandler()).Server);
    }
}
