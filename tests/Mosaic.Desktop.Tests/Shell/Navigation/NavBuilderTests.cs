using FluentIcons.Common;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests.Shell.Navigation;

public sealed class NavBuilderTests
{
    private const string Main = "main";
    private const string Other = "other";

    private static readonly Localizer English = Localizer.Load(LanguageCatalog.Resolve("en"));

    [Fact]
    public void Build_PutsEachScreenUnderItsSection()
    {
        var composition = new DesktopComposition(
            [new NavSectionDefinition(Main, "nav.overview"), new NavSectionDefinition(Other, "nav.administration")],
            [Screen(Main, "first"), Screen(Other, "second"), Screen(Main, "third")]);

        var sections = NavBuilder.Build(composition, English);

        Assert.Equal(["first", "third"], sections[0].Entries.Select(entry => entry.Key));
        Assert.Equal(["second"], sections[1].Entries.Select(entry => entry.Key));
    }

    [Fact]
    public void Build_KeepsSectionOrderFromTheComposition()
    {
        var composition = new DesktopComposition(
            [new NavSectionDefinition(Other, "nav.administration"), new NavSectionDefinition(Main, "nav.overview")],
            [Screen(Main, "open"), Screen(Other, "admin")]);

        var sections = NavBuilder.Build(composition, English);

        Assert.Equal(English.Translate("nav.administration"), sections[0].Caption);
        Assert.Equal(English.Translate("nav.overview"), sections[1].Caption);
    }

    [Fact]
    public void Build_TranslatesLabelsInTheGivenLanguageAndLeavesTheKeyAlone()
    {
        var german = Localizer.Load(LanguageCatalog.Resolve("de"));

        // "Dashboard" is the same word in both languages, so this has to use a key that differs
        var sections = NavBuilder.Build(OneSection(Screen(Main, "nav.settings")), german);
        var entry = sections[0].Entries[0];

        Assert.Equal(german.Translate("nav.settings"), entry.Label);
        Assert.NotEqual(English.Translate("nav.settings"), entry.Label);
        Assert.Equal("nav.settings", entry.Key);
    }

    [Fact]
    public void Build_RefusesACompositionWithNoScreen()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(OneSection(), English));

        Assert.Contains("no screen", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesAContributionNamingAnUndeclaredSection()
    {
        var composition = new DesktopComposition(
            [new NavSectionDefinition(Main, "nav.overview")],
            [Screen(Other, "stray")]);

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, English));

        Assert.Contains(Other, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesTwoContributionsWithOneKey()
    {
        var composition = OneSection(Screen(Main, "same"), Screen(Main, "same"));

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, English));

        Assert.Contains("same", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesASectionDeclaredTwice()
    {
        var composition = new DesktopComposition(
            [new NavSectionDefinition(Main, "nav.overview"), new NavSectionDefinition(Main, "nav.administration")],
            [Screen(Main, "one")]);

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, English));

        Assert.Contains(Main, error.Message, StringComparison.Ordinal);
    }

    private static DesktopComposition OneSection(params NavContribution[] contributions) =>
        new([new NavSectionDefinition(Main, "nav.overview")], contributions);

    private static NavContribution Screen(string section, string key) =>
        new(section, key, Symbol.Board, _ => new FakeScreen());
}
