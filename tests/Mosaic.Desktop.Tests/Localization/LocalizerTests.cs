using System.Globalization;
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

        // TranslateCode treats any known key alike, so this borrows a health key with two placeholders
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

    [Fact]
    public void DateTimeAndOffset_ShowAMomentOnTheViewersClock()
    {
        var berlin = InBerlin("en");
        var moment = new DateTimeOffset(2026, 11, 25, 9, 41, 12, TimeSpan.Zero);

        Assert.Equal("25 Nov 2026", berlin.Date(moment));
        Assert.Equal("10:41:12", berlin.Time(moment));
        Assert.Equal("UTC+01:00", berlin.Offset(moment));
    }

    // 22:30 in UTC on 14 May is half past midnight on 15 May in Berlin, in summer time
    [Fact]
    public void Date_NamesTheMonthInTheAppsLanguage()
    {
        var berlin = InBerlin("de");
        var moment = new DateTimeOffset(2026, 5, 14, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal("15. Mai 2026", berlin.Date(moment));
        Assert.Equal("UTC+02:00", berlin.Offset(moment));
    }

    // Monday 23 November 2026, 9:00 in Berlin; on 25 October Berlin was still on summer time at midnight
    [Theory]
    [InlineData(1, "2026-11-23T00:00:00.0000000+01:00")]
    [InlineData(7, "2026-11-17T00:00:00.0000000+01:00")]
    [InlineData(30, "2026-10-25T00:00:00.0000000+02:00")]
    public void StartOfLastDays_IsMidnightOnTheViewersClock_TodayIncluded(int days, string start)
    {
        var now = new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);

        Assert.Equal(start, InBerlin("en").StartOfLastDays(now, days).ToString("O", CultureInfo.InvariantCulture));
    }

    // 23:30 in UTC is already the next day in Berlin
    [Fact]
    public void StartOfLastDays_CountsTheDaysOfTheViewer_NotOfUtc()
    {
        var now = new DateTimeOffset(2026, 11, 23, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            "2026-11-24T00:00:00.0000000+01:00",
            InBerlin("en").StartOfLastDays(now, 1).ToString("O", CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("en", "1,284")]
    [InlineData("de", "1.284")]
    public void Number_UsesTheThousandsMarkOfTheAppsLanguage(string code, string written)
    {
        Assert.Equal(written, InBerlin(code).Number(1284));
    }

    private static Localizer InBerlin(string code) =>
        Localizer.Load(LanguageCatalog.Resolve(code), TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
}
