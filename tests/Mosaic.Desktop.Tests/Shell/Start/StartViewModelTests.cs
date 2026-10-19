using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Start;

namespace Mosaic.Desktop.Tests.Shell.Start;

public sealed class StartViewModelTests
{
    private static StartViewModel ViewModelWith(HttpMessageHandler handler) =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve("en")));

    [Fact]
    public async Task Check_WhenTheServerAnswers_HandsOverItsRulesOnce()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, RulesBodies.Unclaimed));
        var answers = new List<RulesResult>();

        viewModel.Answered += (_, rules) => answers.Add(rules);

        await viewModel.CheckCommand.ExecuteAsync(null);

        var rules = Assert.Single(answers);

        Assert.Equal(RegistrationState.Unclaimed, rules.Registration);
        Assert.Equal(15, rules.Password.MinLength);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Check_WhenTheServerCannotBeReached_SaysSoAndHandsNothingOver()
    {
        var viewModel = ViewModelWith(new ThrowingHandler(new HttpRequestException("refused")));
        var answered = 0;

        viewModel.Answered += (_, _) => answered++;

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(DesktopErrorCodes.ApiUnreachable, viewModel.ErrorCode);
        Assert.Equal(viewModel.Strings.Translate(DesktopErrorCodes.ApiUnreachable), viewModel.ErrorMessage);
        Assert.Equal(0, answered);
    }

    [Fact]
    public async Task Check_WhenTheServerRefuses_ShowsItsReason()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.TooManyRequests, """{"error":{"code":"error.too_many_requests"}}"""));

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(ErrorCodes.TooManyRequests, viewModel.ErrorCode);
    }

    // an envelope that says success but carries no rules cannot choose a screen
    [Fact]
    public async Task Check_WhenASuccessCarriesNoRules_ReportsUnexpectedResponse()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.OK, "{}"));

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, viewModel.ErrorCode);
    }

    // the view hides the connecting note while HasError is true, so the old reason has to go first
    [Fact]
    public void Check_AgainAfterAFailure_ClearsTheReasonFirst()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());

        viewModel.ErrorCode = DesktopErrorCodes.ApiUnreachable;

        viewModel.CheckCommand.Execute(null);

        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void CheckIfIdle_WhileACheckRuns_SendsNoSecondRequest()
    {
        var handler = new NeverRespondingHandler();
        var viewModel = ViewModelWith(handler);

        viewModel.CheckIfIdle();
        viewModel.CheckIfIdle();

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Server_IsTheHostAndPortTheRequestGoesTo()
    {
        Assert.Equal("localhost:5175", ViewModelWith(new NeverRespondingHandler()).Server);
    }
}
