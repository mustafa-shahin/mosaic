namespace Mosaic.Desktop.Localization;

public sealed record LanguageOption(string Code, string NativeName, bool IsRightToLeft);

// Hand-written in picker order: naming languages found in the embedded json would need GetCultureInfo,
// which throws when InvariantGlobalization is on.
public static class LanguageCatalog
{
    public const string DefaultCode = "en";

    private const string Prefix = "Mosaic.Desktop.Localization.Strings.";
    private const string Suffix = ".json";

    private static readonly LanguageOption English = new(DefaultCode, "English", false);

    public static IReadOnlyList<LanguageOption> Available { get; } =
    [
        English,
        new("de", "Deutsch", false),
        new("ar", "العربية", true),
    ];

    public static string ResourceNameFor(string code) => Prefix + code + Suffix;

    // Exact match first, then language only (de-CH -> de).
    public static LanguageOption Resolve(string? preferred) =>
        Find(preferred) ?? Find(LanguageOf(preferred)) ?? English;

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
}
