using System.Text;
using System.Text.Json;
using Avalonia.Media;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Localization;

// One per language, immutable. Switching language rebuilds the shell with a new one (see App).
public sealed class Localizer
{
    private static readonly Dictionary<string, string> None = [];

    private readonly IReadOnlyDictionary<string, string> _strings;

    private Localizer(LanguageOption language, IReadOnlyDictionary<string, string> strings)
    {
        Language = language;
        _strings = strings;
    }

    public LanguageOption Language { get; }

    public FlowDirection FlowDirection =>
        Language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public bool Has(string key) => _strings.ContainsKey(key);

    // a missing key shows up as the key itself, TranslationDriftTests should catch that
    public string Translate(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var text = _strings.TryGetValue(key, out var found) ? found : key;

        return parameters is null ? text : Fill(text, parameters);
    }

    // unknown code -> generic message. We never display the server's own message text.
    public string TranslateCode(string code, IReadOnlyDictionary<string, string>? parameters = null) =>
        Translate(Has(code) ? code : ErrorCodes.Unexpected, parameters);

    // for XAML bindings, can't bind to a method
    public string this[string key] => Translate(key);

    public static Localizer Load(LanguageOption language) =>
        new(language, StringsFrom(LanguageCatalog.ResourceNameFor(language.Code)));

    // Named placeholders, word order differs between languages.
    private static string Fill(string text, IReadOnlyDictionary<string, string> parameters)
    {
        var builder = new StringBuilder(text);

        foreach (var (name, value) in parameters)
        {
            builder.Replace($"{{{name}}}", value);
        }

        return builder.ToString();
    }

    // Must not throw, runs at startup and on every language change.
    private static Dictionary<string, string> StringsFrom(string resourceName)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            return None;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? None;
        }
        catch (JsonException)
        {
            return None;
        }
    }
}
