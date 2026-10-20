using System.Text.RegularExpressions;

namespace Mosaic.Desktop.Tests.Themes;

// DynamicResource lookups fail silently at runtime, so check the keys here.
public sealed partial class ThemeDriftTests
{
    // boxes for words in the person's own language; every other box holds Latin text such as an address or a token
    private static readonly string[] LanguageBoxes = ["FirstNameBox", "LastNameBox"];

    [Fact]
    public void EveryResourceTheXamlAsksForIsDefined()
    {
        var defined = DefinedKeys();
        var missing = new List<string>();

        foreach (var file in XamlFiles())
        {
            foreach (var match in ResourceReference().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var key = match.Groups[2].Value;

                if (!defined.Contains(key))
                {
                    missing.Add($"{Path.GetFileName(file)} asks for '{key}'");
                }
            }
        }

        Assert.True(missing.Count == 0, $"No such resource: {string.Join("; ", missing)}");
    }

    [Fact]
    public void TheTwoPalettesDefineTheSameNames()
    {
        var night = KeysIn(Palette("AtelierNight"));
        var day = KeysIn(Palette("AtelierDay"));

        Assert.Equal(night.Order(), day.Order());
    }

    [Fact]
    public void NoResourceIsDefinedAndNeverAskedFor()
    {
        var asked = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in XamlFiles())
        {
            foreach (var match in ResourceReference().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                asked.Add(match.Groups[2].Value);
            }
        }

        var unused = DefinedKeys().Where(key => !asked.Contains(key)).Order().ToList();

        Assert.True(unused.Count == 0, $"Defined but never asked for: {string.Join(", ", unused)}");
    }

    // same for classes: Classes="foo" with no .foo selector anywhere just renders unstyled. Names only.
    [Fact]
    public void EveryClassAskedForInXamlHasAStyle()
    {
        var styled = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in XamlFiles())
        {
            foreach (var selector in StyleSelector().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                foreach (var name in SelectorClass().Matches(selector.Groups[1].Value).Cast<Match>())
                {
                    styled.Add(name.Groups[1].Value);
                }
            }
        }

        var missing = new List<string>();

        foreach (var file in XamlFiles())
        {
            foreach (var use in ClassUse().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                // two forms: Classes="a b" and Classes.a="{Binding ...}"
                var bound = use.Groups[1].Value;
                string[] names = bound.Length > 0
                    ? [bound]
                    : use.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var name in names)
                {
                    if (!styled.Contains(name))
                    {
                        missing.Add($"{Path.GetFileName(file)} asks for '{name}'");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, $"No style selects: {string.Join("; ", missing)}");
    }

    // A colour written into a view cannot follow the theme.
    [Fact]
    public void NoColourLiteralOutsideThePalettes()
    {
        var offenders = new List<string>();

        foreach (var file in XamlFiles())
        {
            if (file.StartsWith(PalettesFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var match in ColourLiteral().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                offenders.Add($"{Path.GetFileName(file)} writes '{match.Value}'");
            }
        }

        Assert.True(offenders.Count == 0, $"Colour outside the palettes: {string.Join("; ", offenders)}");
    }

    // No specificity in Avalonia, so the reset only works while nothing sets LetterSpacing after it.
    [Fact]
    public void TheRightToLeftResetComesAfterEveryOtherLetterSpacing()
    {
        var lines = File.ReadAllLines(Path.Combine(RepoLayout.Themes, "Controls.axaml"));

        var reset = Array.FindIndex(lines, line => line.Contains(":is(TextBlock)[FlowDirection=RightToLeft]", StringComparison.Ordinal));

        Assert.True(reset >= 0, "The right-to-left letter-spacing reset is no longer in Controls.axaml.");

        var setters = new List<int>();

        for (var line = 0; line < lines.Length; line++)
        {
            if (lines[line].Contains("Property=\"LetterSpacing\"", StringComparison.Ordinal))
            {
                setters.Add(line);
            }
        }

        // the reset's own setter is the first one below its selector, so skip it; nothing may follow
        var late = setters.Where(line => line > reset).Skip(1).Select(line => line + 1).ToList();

        Assert.True(late.Count == 0, $"LetterSpacing is set after the right-to-left reset, on line {string.Join(", ", late)}");
    }

    // a box without the class draws Latin text out of order in Arabic, so every new box has to choose
    [Fact]
    public void EveryTextBoxRunsLeftToRightExceptTheNames()
    {
        var missing = new List<string>();

        foreach (var file in XamlFiles())
        {
            foreach (var box in TextBoxElement().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var name = ElementName().Match(box.Value).Groups[1].Value;
                var classes = ClassUse().Match(box.Value).Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (!classes.Contains("ltr") && !LanguageBoxes.Contains(name))
                {
                    missing.Add($"{Path.GetFileName(file)} {name}");
                }
            }
        }

        Assert.True(missing.Count == 0, $"No ltr class and not a box for the person's own language: {string.Join("; ", missing)}");
    }

    private static HashSet<string> DefinedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(RepoLayout.Themes, "*.axaml", SearchOption.AllDirectories))
        {
            keys.UnionWith(KeysIn(file));
        }

        return keys;
    }

    // skip the Dark/Light dictionaries and the ControlThemes keyed by type
    private static IEnumerable<string> KeysIn(string file) =>
        DefinedKey().Matches(File.ReadAllText(file))
            .Cast<Match>()
            .Where(match => match.Groups[1].Value != "ResourceDictionary")
            .Select(match => match.Groups[2].Value)
            .Where(key => !key.StartsWith('{'));

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(RepoLayout.Desktop, "*.axaml", SearchOption.AllDirectories);

    private static string Palette(string name) =>
        Path.Combine(RepoLayout.Themes, "Palettes", name + ".axaml");

    private static string PalettesFolder => Path.Combine(RepoLayout.Themes, "Palettes");

    [GeneratedRegex(@"\{(DynamicResource|StaticResource)\s+([^}]+)\}")]
    private static partial Regex ResourceReference();

    [GeneratedRegex(@"<([\w:]+)[^>]*?\sx:Key=""([^""]+)""")]
    private static partial Regex DefinedKey();

    [GeneratedRegex(@"Selector=""([^""]+)""")]
    private static partial Regex StyleSelector();

    [GeneratedRegex(@"\.([A-Za-z][\w-]*)")]
    private static partial Regex SelectorClass();

    [GeneratedRegex(@"\bClasses(?:\.(\w+))?=""([^""]+)""")]
    private static partial Regex ClassUse();

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex ColourLiteral();

    [GeneratedRegex(@"<TextBox\s[^>]*>")]
    private static partial Regex TextBoxElement();

    [GeneratedRegex(@"\sName=""([^""]+)""")]
    private static partial Regex ElementName();
}
