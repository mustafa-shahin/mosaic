using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Tests.Localization;

public sealed class LanguageChoiceTests : IDisposable
{
    private static readonly LanguageOption English = LanguageCatalog.Resolve("en");
    private static readonly LanguageOption German = LanguageCatalog.Resolve("de");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private SettingsStore Store() => new(_folder);

    [Fact]
    public void Choose_AnotherLanguage_RemembersItSavesItAndTellsTheListeners()
    {
        var choice = new LanguageChoice(Store(), English);
        var told = new List<string>();

        choice.Changed += (_, language) => told.Add(language.Code);

        choice.Choose(German);

        Assert.Equal(German, choice.Chosen);
        Assert.Equal("de", Store().Read().LanguageCode);
        Assert.Equal(["de"], told);
    }

    [Fact]
    public void Choose_TheLanguageAlreadyChosen_SavesNothingAndTellsNobody()
    {
        var choice = new LanguageChoice(Store(), English);
        var told = new List<string>();

        choice.Changed += (_, language) => told.Add(language.Code);

        choice.Choose(English);

        Assert.False(File.Exists(Path.Combine(_folder, "settings.json")));
        Assert.Empty(told);
    }

    [Fact]
    public void Choose_KeepsTheStoredAppearance()
    {
        Store().Write(new DesktopSettings { Appearance = Appearances.Light });

        new LanguageChoice(Store(), English).Choose(German);

        Assert.Equal(Appearances.Light, Store().Read().Appearance);
        Assert.Equal("de", Store().Read().LanguageCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
