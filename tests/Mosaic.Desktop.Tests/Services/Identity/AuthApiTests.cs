using System.Net;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services.Identity;

public sealed class AuthApiTests
{
    // a literal on purpose: an installed desktop sends exactly this, so renaming a route constant must fail here
    [Fact]
    public async Task SignIn_PostsToTheV1Address()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, SignInBodies.Accepted);
        var auth = new AuthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        await auth.SignInAsync(new SignInRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/sign-in", recorder.Path);
    }

    [Fact]
    public async Task Rules_GetsTheV1Address()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, RulesBodies.Closed);
        var auth = new AuthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        await auth.GetRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, recorder.Method);
        Assert.Equal("/v1/auth/rules", recorder.Path);
    }

    [Fact]
    public async Task Rules_ReadsTheAnswerTheApiWrites()
    {
        var auth = new AuthApi(new ApiClient(
            new HttpClient(new CannedResponseHandler(HttpStatusCode.OK, RulesBodies.Unclaimed)) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        var response = await auth.GetRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationState.Unclaimed, response.Data!.Registration);
        Assert.Equal(15, response.Data.Password.MinLength);
        Assert.Equal(128, response.Data.Password.MaxLength);
    }

    // an enum name this build lacks fails the whole read, and ApiClient turns that into UnexpectedResponse
    [Fact]
    public async Task Rules_WithAValueThisBuildDoesNotKnow_IsAnUnexpectedResponse()
    {
        var newer = """{"data":{"registration":"InviteOnly","password":{"minLength":15,"maxLength":128}},"success":true}""";
        var auth = new AuthApi(new ApiClient(
            new HttpClient(new CannedResponseHandler(HttpStatusCode.OK, newer)) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        var response = await auth.GetRulesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopErrorCodes.UnexpectedResponse, response.Error!.Code);
    }

    [Fact]
    public async Task Register_PostsToTheV1Address()
    {
        var recorder = new RecordingHandler(HttpStatusCode.Created, RegisterBodies.Owner);
        var auth = new AuthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

        await auth.RegisterAsync(new RegisterRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/register", recorder.Path);
    }

    [Fact]
    public async Task SignOut_PostsToTheV1AddressWithNoBody()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var session = new CurrentSession();
        var auth = new AuthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, session));

        await auth.SignOutAsync(session.Start(SignInResults.Ada()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/sign-out", recorder.Path);
        Assert.Null(recorder.Body);
    }

    [Fact]
    public async Task SignOutEverywhere_PostsToTheV1AddressWithNoBody()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var session = new CurrentSession();
        var auth = new AuthApi(new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://localhost:5175/") }, session));

        await auth.SignOutEverywhereAsync(session.Start(SignInResults.Ada()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, recorder.Method);
        Assert.Equal("/v1/auth/sign-out-everywhere", recorder.Path);
        Assert.Null(recorder.Body);
    }

    [Fact]
    public void BaseAddress_IsTheAddressOfTheClientUnderneath()
    {
        var auth = new AuthApi(new ApiClient(new HttpClient { BaseAddress = new Uri("https://core.acme.internal:8443/") }, new CurrentSession()));

        Assert.Equal(new Uri("https://core.acme.internal:8443/"), auth.BaseAddress);
    }
}
