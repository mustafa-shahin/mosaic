using System.Net;
using Mosaic.Contracts;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Features.Settings;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));
    private readonly CurrentSession _session = new();
    private readonly SettingsViewModel _settings;

    public SettingsViewModelTests()
    {
        _settings = new SettingsViewModel(_strings, new SettingsStore(_folder), SignOutOver(new NeverRespondingHandler()));
    }

    private SignOut SignOutOver(HttpMessageHandler handler) =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, _session)), _session);

    // simulates a restart or shell rebuild
    private SettingsViewModel Reopened() => new(_strings, new SettingsStore(_folder), SignOutOver(new NeverRespondingHandler()));

    private SettingsViewModel SignedInWith(HttpMessageHandler server)
    {
        _session.Start(SignInResults.Ada());

        return new SettingsViewModel(_strings, new SettingsStore(_folder), SignOutOver(server));
    }

    private DesktopSettings Stored() => new SettingsStore(_folder).Read();

    [Fact]
    public void Languages_AreEveryOneTheAppShips()
    {
        Assert.Equal(LanguageCatalog.Available, _settings.Languages);
    }

    [Fact]
    public void SelectedLanguage_StartsAtTheLanguageInForce()
    {
        Assert.Equal("en", _settings.SelectedLanguage!.Code);
    }

    // While nothing is stored, each start follows the OS language, so building the view model must not store it.
    [Fact]
    public void Construct_WritesNothing_SinceStartingUpIsNotChoosing()
    {
        Assert.Null(Stored().LanguageCode);
        Assert.False(File.Exists(Path.Combine(_folder, "settings.json")));
    }

    [Fact]
    public void SelectedLanguage_WhenChosen_RemembersItAndAsksForARebuild()
    {
        var asked = new List<string>();
        _settings.LanguageChosen += (_, language) => asked.Add(language.Code);

        _settings.SelectedLanguage = LanguageCatalog.Resolve("ar");

        Assert.Equal(["ar"], asked);
        Assert.Equal("ar", Stored().LanguageCode);
    }

    [Fact]
    public void SelectedLanguage_WhenChosenAgain_AsksForNothing()
    {
        var asked = new List<string>();
        _settings.LanguageChosen += (_, language) => asked.Add(language.Code);

        _settings.SelectedLanguage = LanguageCatalog.Resolve("en");

        Assert.Empty(asked);
    }

    [Fact]
    public void IsDark_WithNothingStored_StartsDark()
    {
        Assert.True(_settings.IsDark);
        Assert.Equal(_strings.Translate("settings.appearance_dark"), _settings.AppearanceName);
    }

    [Fact]
    public void AppearanceAction_OffersTheOppositeOfWhereYouAre()
    {
        Assert.Equal(_strings.Translate("settings.switch_to_light"), _settings.AppearanceAction);

        _settings.ToggleAppearanceCommand.Execute(null);

        Assert.Equal(_strings.Translate("settings.switch_to_dark"), _settings.AppearanceAction);
    }

    [Fact]
    public void ToggleAppearance_FlipsTheModeRemembersItAndSurvivesARebuild()
    {
        _settings.ToggleAppearanceCommand.Execute(null);

        Assert.False(_settings.IsDark);
        Assert.Equal(Appearances.Light, Stored().Appearance);
        Assert.False(Reopened().IsDark);

        _settings.ToggleAppearanceCommand.Execute(null);

        Assert.True(_settings.IsDark);
        Assert.Equal(Appearances.Dark, Stored().Appearance);
    }

    [Fact]
    public void SelectedLanguage_WhenChosen_KeepsTheAppearance()
    {
        _settings.ToggleAppearanceCommand.Execute(null);

        _settings.SelectedLanguage = LanguageCatalog.Resolve("de");

        Assert.Equal(Appearances.Light, Stored().Appearance);
        Assert.Equal("de", Stored().LanguageCode);
    }

    [Fact]
    public async Task SignOutEverywhere_WhenTheServerConfirms_EndsTheSessionAndShowsNoError()
    {
        var settings = SignedInWith(new RecordingHandler(HttpStatusCode.OK, """{"success":true}"""));

        await settings.SignOutEverywhereCommand.ExecuteAsync(null);

        Assert.Null(_session.Current);
        Assert.False(settings.HasError);
    }

    [Fact]
    public async Task SignOutEverywhere_WhenTheServerFails_KeepsTheSessionAndShowsWhy()
    {
        var settings = SignedInWith(new CannedResponseHandler(HttpStatusCode.InternalServerError, """{"error":{"code":"error.unexpected"}}"""));

        await settings.SignOutEverywhereCommand.ExecuteAsync(null);

        Assert.NotNull(_session.Current);
        Assert.Equal(ErrorCodes.Unexpected, settings.ErrorCode);
        Assert.Equal(_strings.Translate(ErrorCodes.Unexpected), settings.ErrorMessage);
    }

    [Fact]
    public async Task SignOutEverywhere_ClearsTheReasonOfTheLastAttemptFirst()
    {
        var settings = SignedInWith(new RecordingHandler(HttpStatusCode.OK, """{"success":true}"""));

        settings.ErrorCode = DesktopErrorCodes.ApiUnreachable;

        await settings.SignOutEverywhereCommand.ExecuteAsync(null);

        Assert.Null(settings.ErrorCode);
    }

    // the button is bound to the command, so this is what shows the person that it is still waiting
    [Fact]
    public void SignOutEverywhere_WhileTheServerHasNotAnswered_CannotStartASecond()
    {
        var settings = SignedInWith(new NeverRespondingHandler());

        settings.SignOutEverywhereCommand.Execute(null);

        Assert.True(settings.SignOutEverywhereCommand.IsRunning);
        Assert.False(settings.SignOutEverywhereCommand.CanExecute(null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
