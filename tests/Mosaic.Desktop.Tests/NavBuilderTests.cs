using FluentIcons.Common;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests;

// The filter and grouping rules live only in NavBuilder, so this is the only place that proves them.
public sealed class NavBuilderTests
{
    private const string Main = "main";
    private const string Other = "other";

    private static readonly Localizer English = Localizer.Load(LanguageCatalog.Resolve("en"));

    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>(StringComparer.Ordinal);

    [Fact]
    public void Build_ShowsALocalEntryWithoutASession()
    {
        var sections = NavBuilder.Build(OneSection(Local("open")), Nothing, English);

        Assert.Equal(["open"], Shown(sections));
    }

    [Fact]
    public void Build_HidesAServedEntryTheSessionDidNotList()
    {
        var sections = NavBuilder.Build(OneSection(Local("open"), Served("gated")), Nothing, English);

        Assert.Equal(["open"], Shown(sections));
    }

    [Fact]
    public void Build_ShowsAServedEntryTheSessionListed()
    {
        var sections = NavBuilder.Build(OneSection(Local("open"), Served("gated")), Listing("gated"), English);

        Assert.Equal(["open", "gated"], Shown(sections));
    }

    // a newer server, or a pro id arriving at a core desktop
    [Fact]
    public void Build_IgnoresAListedIdTheCompositionLacks()
    {
        var sections = NavBuilder.Build(OneSection(Local("open")), Listing("nav.unknown"), English);

        Assert.Equal(["open"], Shown(sections));
    }

    [Fact]
    public void Build_FiltersChildrenWithTheSameRuleAsParents()
    {
        var composition = OneSection(Parent("parent", Local("visible"), Served("hidden")));

        var sections = NavBuilder.Build(composition, Nothing, English);
        var parent = sections[0].Entries[0];

        Assert.Single(parent.Children);
        Assert.Equal("visible", parent.Children[0].Key);
    }

    [Fact]
    public void Build_DropsASectionWithNothingVisible()
    {
        var composition = new FakeComposition(
            [new NavSectionDefinition(Main, "nav.overview"), new NavSectionDefinition(Other, "nav.administration")],
            [Local("open"), ServedIn(Other, "private")]);

        var sections = NavBuilder.Build(composition, Nothing, English);

        Assert.Single(sections);
        Assert.Equal(English.Translate("nav.overview"), sections[0].Caption);
    }

    [Fact]
    public void Build_KeepsSectionOrderFromTheComposition()
    {
        var composition = new FakeComposition(
            [new NavSectionDefinition(Other, "nav.administration"), new NavSectionDefinition(Main, "nav.overview")],
            [Local("open"), LocalIn(Other, "admin")]);

        var sections = NavBuilder.Build(composition, Nothing, English);

        Assert.Equal(English.Translate("nav.administration"), sections[0].Caption);
        Assert.Equal(English.Translate("nav.overview"), sections[1].Caption);
    }

    [Fact]
    public void Build_TranslatesLabelsInTheGivenLanguageAndLeavesTheKeyAlone()
    {
        var german = Localizer.Load(LanguageCatalog.Resolve("de"));

        // "Dashboard" is the same word in both languages, so this has to use a key that differs
        var sections = NavBuilder.Build(OneSection(Local("nav.settings")), Nothing, german);
        var entry = sections[0].Entries[0];

        Assert.Equal(german.Translate("nav.settings"), entry.Label);
        Assert.NotEqual(English.Translate("nav.settings"), entry.Label);
        Assert.Equal("nav.settings", entry.Key);
    }

    [Fact]
    public void Build_RefusesAContributionNamingAnUndeclaredSection()
    {
        var composition = new FakeComposition(
            [new NavSectionDefinition(Main, "nav.overview")],
            [LocalIn(Other, "stray")]);

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, Nothing, English));

        Assert.Contains(Other, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesTwoContributionsWithOneKey()
    {
        var composition = OneSection(Local("same"), Local("same"));

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, Nothing, English));

        Assert.Contains("same", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesASectionDeclaredTwice()
    {
        var composition = new FakeComposition(
            [new NavSectionDefinition(Main, "nav.overview"), new NavSectionDefinition(Main, "nav.administration")],
            [Local("one")]);

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, Nothing, English));

        Assert.Contains(Main, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RefusesAChildThatNamesAnotherSectionThanItsParent()
    {
        var composition = OneSection(Parent("parent", LocalIn(Other, "stray")));

        var error = Assert.Throws<InvalidOperationException>(
            () => NavBuilder.Build(composition, Nothing, English));

        Assert.Contains("stray", error.Message, StringComparison.Ordinal);
        Assert.Contains("parent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Flatten_ReturnsEveryEntryAtEveryDepth()
    {
        var composition = OneSection(Parent("parent", Local("child")), Local("sibling"));

        var all = NavBuilder.Flatten(NavBuilder.Build(composition, Nothing, English));

        Assert.Equal(["parent", "child", "sibling"], all.Select(entry => entry.Key));
    }

    [Fact]
    public void Keys_ReturnsEveryContributedKeyAtEveryDepthIncludingServedOnes()
    {
        var composition = OneSection(Parent("parent", Served("child")), Local("sibling"));

        Assert.Equal(["child", "parent", "sibling"], NavBuilder.Keys(composition).Order());
    }

    private static FakeComposition OneSection(params NavContribution[] contributions) =>
        new([new NavSectionDefinition(Main, "nav.overview")], contributions);

    // grouping and validation do not depend on visibility, so those tests use local entries and an empty listing
    private static NavContribution Local(string key) => LocalIn(Main, key);

    private static NavContribution LocalIn(string section, string key) =>
        new(section, key, Symbol.Board, local: true, _ => new FakeScreen());

    private static NavContribution Served(string key) => ServedIn(Main, key);

    private static NavContribution ServedIn(string section, string key) =>
        new(section, key, Symbol.Board, local: false, _ => new FakeScreen());

    private static NavContribution Parent(string key, params NavContribution[] children) =>
        new(Main, key, Symbol.Board, Local: true, _ => new FakeScreen(), children);

    private static HashSet<string> Listing(params string[] ids) => ids.ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> Shown(IReadOnlyList<NavSection> sections) =>
        sections.SelectMany(section => section.Entries).Select(entry => entry.Key);
}
