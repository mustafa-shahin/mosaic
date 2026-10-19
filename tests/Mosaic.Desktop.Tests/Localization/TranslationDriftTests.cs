using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mosaic.Contracts;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Tests.Localization;

// keeps Strings.*.json and the code in sync
public sealed partial class TranslationDriftTests
{
    public static TheoryData<string> ShippedLanguages()
    {
        var data = new TheoryData<string>();

        foreach (var language in LanguageCatalog.Available)
        {
            data.Add(language.Code);
        }

        return data;
    }

    [Fact]
    public void EnglishIsAlwaysShipped()
    {
        Assert.Contains(LanguageCatalog.Available, language => language.Code == LanguageCatalog.DefaultCode);
    }

    // an undeclared json file never reaches the picker, and a declared language with no file shows bare keys
    [Fact]
    public void TheEmbeddedFilesAreExactlyTheDeclaredLanguages()
    {
        var declared = LanguageCatalog.Available
            .Select(language => LanguageCatalog.ResourceNameFor(language.Code))
            .Order()
            .ToList();

        // prefix too, not just .json, so an unrelated embedded json doesn't fail this
        const string prefix = "Mosaic.Desktop.Localization.Strings.";

        var embedded = typeof(LanguageCatalog).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .Order()
            .ToList();

        Assert.Equal(declared, embedded);
    }

    [Fact]
    public void EveryErrorCodeHasAnEnglishSentence()
    {
        var english = Strings(LanguageCatalog.DefaultCode);
        var missing = new List<string>();

        foreach (var code in DeclaredCodes())
        {
            if (!english.ContainsKey(code))
            {
                missing.Add(code);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"No English translation for: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguages))]
    public void EveryLanguageCarriesTheSameKeysAsEnglish(string code)
    {
        var english = Strings(LanguageCatalog.DefaultCode).Keys;
        var translated = Strings(code).Keys;

        var missing = english.Except(translated).Order().ToList();
        var extra = translated.Except(english).Order().ToList();

        Assert.True(
            missing.Count == 0 && extra.Count == 0,
            $"'{code}' is out of step with English. Missing: [{string.Join(", ", missing)}]. " +
            $"Not in English: [{string.Join(", ", extra)}].");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguages))]
    public void EveryLanguageUsesTheSamePlaceholdersAsEnglish(string code)
    {
        var english = Strings(LanguageCatalog.DefaultCode);
        var wrong = new List<string>();

        foreach (var (key, text) in Strings(code))
        {
            // a key English lacks is reported by EveryLanguageCarriesTheSameKeysAsEnglish
            if (!english.TryGetValue(key, out var source))
            {
                continue;
            }

            var expected = Placeholders(source);
            var found = Placeholders(text);

            if (!expected.SetEquals(found))
            {
                wrong.Add($"'{key}' has [{string.Join(", ", found.Order())}] where English has [{string.Join(", ", expected.Order())}]");
            }
        }

        Assert.True(wrong.Count == 0, $"'{code}' fills the wrong names: {string.Join("; ", wrong)}");
    }

    // the desktop fills {name} from the parameter of that name, so a name the server never sends stays on screen
    [Fact]
    public void EveryServerErrorUsesOnlyParametersTheServerSends()
    {
        var english = Strings(LanguageCatalog.DefaultCode);
        var sent = ConstantsOf(typeof(ErrorParams)).ToHashSet(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (var code in ConstantsOf(typeof(ErrorCodes)))
        {
            // a missing sentence is reported by EveryErrorCodeHasAnEnglishSentence
            if (!english.TryGetValue(code, out var text))
            {
                continue;
            }

            foreach (var name in Placeholders(text))
            {
                if (!sent.Contains(name))
                {
                    unknown.Add($"'{code}' uses {{{name}}}");
                }
            }
        }

        Assert.True(unknown.Count == 0, $"Not a parameter the server sends: {string.Join("; ", unknown)}");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguages))]
    public void NoTranslationIsBlank(string code)
    {
        foreach (var (key, text) in Strings(code))
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"'{code}' has an empty value for '{key}'.");
        }
    }

    [Fact]
    public void EveryKeyBoundFromXamlHasATranslation()
    {
        var english = Strings(LanguageCatalog.DefaultCode);
        var views = RepoLayout.Desktop;

        Assert.True(Directory.Exists(views), $"Could not find the view folder at '{views}'.");

        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (var match in BoundKey().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var key = match.Groups[1].Value;

                if (!english.ContainsKey(key))
                {
                    missing.Add($"{Path.GetFileName(file)} binds '{key}'");
                }
            }
        }

        Assert.True(missing.Count == 0, $"No translation for: {string.Join("; ", missing)}");
    }

    [Fact]
    public void EveryKeyAskedForFromCodeHasATranslation()
    {
        var english = Strings(LanguageCatalog.DefaultCode);
        var source = RepoLayout.Desktop;
        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var match in TranslatedKey().Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var key = match.Groups[1].Value;

                if (!english.ContainsKey(key))
                {
                    missing.Add($"{Path.GetFileName(file)} asks for '{key}'");
                }
            }
        }

        Assert.True(missing.Count == 0, $"No translation for: {string.Join("; ", missing)}");
    }

    // the reverse check; it matches the quoted key anywhere in src, so a key held in a const counts as asked for
    [Fact]
    public void EveryEnglishKeyIsAskedForSomewhere()
    {
        var source = AllHandWrittenSource();
        var unused = new List<string>();

        foreach (var key in Strings(LanguageCatalog.DefaultCode).Keys)
        {
            if (!source.Contains($"\"{key}\"", StringComparison.Ordinal) &&
                !source.Contains($"[{key}]", StringComparison.Ordinal))
            {
                unused.Add(key);
            }
        }

        Assert.True(unused.Count == 0, $"Nothing asks for: {string.Join(", ", unused)}");
    }

    // all of src, the error codes live in Mosaic.Contracts
    private static string AllHandWrittenSource()
    {
        var all = new StringBuilder();

        foreach (var file in Directory.EnumerateFiles(RepoLayout.Source, "*.*", SearchOption.AllDirectories))
        {
            if (IsHandWritten(file))
            {
                all.Append(File.ReadAllText(file));
            }
        }

        return all.ToString();
    }

    private static bool IsHandWritten(string file) =>
        (file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".axaml", StringComparison.Ordinal))
        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static HashSet<string> Placeholders(string text) =>
        Placeholder().Matches(text)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"Strings\[([^\]]+)\]")]
    private static partial Regex BoundKey();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"Translate\(""([^""]+)""")]
    private static partial Regex TranslatedKey();

    private static IEnumerable<string> DeclaredCodes()
    {
        foreach (var code in ConstantsOf(typeof(ErrorCodes)))
        {
            yield return code;
        }

        foreach (var code in ConstantsOf(typeof(DesktopErrorCodes)))
        {
            yield return code;
        }
    }

    private static IEnumerable<string> ConstantsOf(Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            {
                yield return (string)field.GetRawConstantValue()!;
            }
        }
    }

    // read the json directly, Localizer would mask missing keys by returning the key itself
    private static Dictionary<string, string> Strings(string code)
    {
        var name = LanguageCatalog.ResourceNameFor(code);

        using var stream = typeof(LanguageCatalog).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No embedded translations named '{name}'.");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"The translations in '{name}' are empty.");
    }
}
