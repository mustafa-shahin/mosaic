using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Tests.Localization;

public sealed class LanguageCatalogTests
{
    [Fact]
    public void Resolve_ForArabic_IsRightToLeft()
    {
        Assert.True(LanguageCatalog.Resolve("ar").IsRightToLeft);
        Assert.False(LanguageCatalog.Resolve("de").IsRightToLeft);
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE", "de")]
    public void Resolve_WithAnExactMatch_TakesItWhateverTheCasing(string preferred, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.Resolve(preferred).Code);
    }

    [Theory]
    [InlineData("de-CH", "de")]
    [InlineData("ar-EG", "ar")]
    public void Resolve_WithARegion_FallsBackToTheLanguage(string preferred, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.Resolve(preferred).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fr")]
    [InlineData("not a culture")]
    public void Resolve_WithAnythingNotShipped_FallsBackToEnglish(string? preferred)
    {
        Assert.Equal("en", LanguageCatalog.Resolve(preferred).Code);
    }

    // Runs at startup, must not throw.
    [Fact]
    public void Load_ForALanguageWithNoFileBehindIt_StillLoads()
    {
        var localizer = Localizer.Load(new LanguageOption("zz", "Nothing", false));

        Assert.False(localizer.Has("app.title"));
        Assert.Equal("app.title", localizer.Translate("app.title"));
    }
}
