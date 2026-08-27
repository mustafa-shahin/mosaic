using System.Globalization;

namespace Mosaic.Desktop.Localization;

public sealed record LanguageOption(string Code, string NativeName, bool IsRightToLeft);

// One language per embedded Strings.<code>.json, nothing else to register. TranslationDriftTests checks the keys.
public static class LanguageCatalog
{
    public const string DefaultCode = "en";

    private const string Prefix = "Mosaic.Desktop.Localization.Strings.";
    private const string Suffix = ".json";

    // in case no json got embedded at all (broken build)
    private static readonly LanguageOption Fallback = new(DefaultCode, "English", false);

    public static IReadOnlyList<LanguageOption> Available { get; } = Discover();

    public static string ResourceNameFor(string code) => Prefix + code + Suffix;

    // Exact match first, then language only (de-CH -> de).
    public static LanguageOption Resolve(string? preferred) =>
        Find(preferred)
        ?? Find(LanguageOf(preferred))
        ?? Find(DefaultCode)
        ?? (Available.Count > 0 ? Available[0] : Fallback);

    private static LanguageOption? Find(string? code) =>
        code is null
            ? null
            : Available.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    private static string? LanguageOf(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var separator = code.IndexOf('-', StringComparison.Ordinal);

        return separator < 0 ? null : code[..separator];
    }

    private static List<LanguageOption> Discover()
    {
        var found = new List<LanguageOption>();

        foreach (var name in typeof(LanguageCatalog).Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !name.EndsWith(Suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var language = Describe(name[Prefix.Length..^Suffix.Length]);

            if (language is not null)
            {
                found.Add(language);
            }
        }

        found.Sort((left, right) => string.CompareOrdinal(left.Code, right.Code));

        return found;
    }

    // Called from the static initializer, must not throw.
    private static LanguageOption? Describe(string code)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);

            return new LanguageOption(code, culture.NativeName, culture.TextInfo.IsRightToLeft);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
