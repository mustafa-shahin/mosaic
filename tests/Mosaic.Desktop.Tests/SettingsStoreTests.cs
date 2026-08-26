using Mosaic.Desktop.Settings;

namespace Mosaic.Desktop.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private SettingsStore Store() => new(_folder);

    [Fact]
    public void Read_WithNothingStoredYet_MeansNoLanguageChosen()
    {
        Assert.Null(Store().Read().LanguageCode);
    }

    [Fact]
    public void Read_AfterAWrite_ReturnsTheChoice()
    {
        Store().Write(new DesktopSettings { LanguageCode = "ar" });

        Assert.Equal("ar", Store().Read().LanguageCode);
    }

    [Fact]
    public void Write_PutsTheFileInTheWireFormat()
    {
        Store().Write(new DesktopSettings { LanguageCode = "de" });

        var json = File.ReadAllText(Path.Combine(_folder, "settings.json"));

        Assert.Contains("\"languageCode\": \"de\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WithAHandEditedFile_IsNotFussyAboutCasing()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), """{"LanguageCode":"ar"}""");

        Assert.Equal("ar", Store().Read().LanguageCode);
    }

    [Fact]
    public void Read_WithAFileThatIsNotJson_CostsThePreferenceAndNothingElse()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "this is not json {{{");

        Assert.Null(Store().Read().LanguageCode);
    }

    [Fact]
    public void Write_WhenTheFolderCannotBeMade_CostsThePreferenceAndNothingElse()
    {
        Directory.CreateDirectory(_folder);

        // a file in the way of the directory
        var wall = Path.Combine(_folder, "wall");
        File.WriteAllText(wall, string.Empty);

        var blocked = new SettingsStore(Path.Combine(wall, "nested"));

        blocked.Write(new DesktopSettings { LanguageCode = "de" });

        Assert.Null(blocked.Read().LanguageCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
