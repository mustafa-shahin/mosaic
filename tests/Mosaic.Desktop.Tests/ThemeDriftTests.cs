using System.Text.RegularExpressions;

namespace Mosaic.Desktop.Tests;

// DynamicResource lookups fail silently at runtime, so check the keys here.
public sealed partial class ThemeDriftTests
{
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

    [GeneratedRegex(@"\{(DynamicResource|StaticResource)\s+([^}]+)\}")]
    private static partial Regex ResourceReference();

    [GeneratedRegex(@"<([\w:]+)[^>]*?\sx:Key=""([^""]+)""")]
    private static partial Regex DefinedKey();
}
