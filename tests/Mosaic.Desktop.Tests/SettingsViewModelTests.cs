using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Settings;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));
    private readonly SettingsViewModel _settings;

    public SettingsViewModelTests()
    {
        _settings = new SettingsViewModel(_strings, new SettingsStore(_folder));
    }

    // simulates a restart or shell rebuild
    private SettingsViewModel Reopened() => new(_strings, new SettingsStore(_folder));

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

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
