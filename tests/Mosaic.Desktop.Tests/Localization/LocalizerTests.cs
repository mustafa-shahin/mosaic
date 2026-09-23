using Avalonia.Media;
using Mosaic.Contracts;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Tests.Localization;

public sealed class LocalizerTests
{
    private static Localizer In(string code) => Localizer.Load(LanguageCatalog.Resolve(code));

    [Fact]
    public void Translate_WithNamedValues_PutsThemIntoTheSentence()
    {
        var sentence = In("en").Translate("health.contract_mismatch", new Dictionary<string, string>
        {
            ["desktop"] = "1.0",
            ["server"] = "2.0"
        });

        Assert.Equal("This desktop speaks 1.0. The server speaks 2.0.", sentence);
    }

    [Fact]
    public void TranslateCode_ForACodeThisBuildDoesNotKnow_FallsBackToUnexpected()
    {
        var localizer = In("en");

        Assert.Equal(localizer.Translate(ErrorCodes.Unexpected), localizer.TranslateCode("something.the.server.made.up"));
    }

    [Fact]
    public void TranslateCode_ForAKnownCode_TranslatesItWithTheValuesThatCameWithIt()
    {
        var localizer = In("en");

        Assert.Equal(
            localizer.Translate(DesktopErrorCodes.ApiUnreachable),
            localizer.TranslateCode(DesktopErrorCodes.ApiUnreachable));

        // none of the error codes have placeholders yet, so borrow a key that does
        var withValues = localizer.TranslateCode("health.contract_mismatch", new Dictionary<string, string>
        {
            ["desktop"] = "1.0",
            ["server"] = "2.0"
        });

        Assert.Equal("This desktop speaks 1.0. The server speaks 2.0.", withValues);
    }

    [Fact]
    public void Load_ForAnotherLanguage_ReturnsDifferentWordsForTheSameKey()
    {
        Assert.NotEqual(In("en")["health.check_again"], In("de")["health.check_again"]);
    }

    [Fact]
    public void FlowDirection_ForArabic_IsRightToLeft()
    {
        Assert.Equal(FlowDirection.LeftToRight, In("en").FlowDirection);
        Assert.Equal(FlowDirection.RightToLeft, In("ar").FlowDirection);
    }
}
