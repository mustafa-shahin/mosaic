using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia.Media;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Localization;

// One per language, immutable. Switching language rebuilds the shell with a new one (see MainWindowViewModel).
public sealed class Localizer
{
    private static readonly Dictionary<string, string> None = [];

    private readonly IReadOnlyDictionary<string, string> _strings;
    private readonly CultureInfo _culture;
    private readonly TimeZoneInfo _zone;

    private Localizer(LanguageOption language, IReadOnlyDictionary<string, string> strings, TimeZoneInfo zone)
    {
        Language = language;
        _strings = strings;
        _culture = CultureOf(language);
        _zone = zone;
    }

    public LanguageOption Language { get; }

    public FlowDirection FlowDirection =>
        Language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public bool Has(string key) => _strings.ContainsKey(key);

    // a missing key shows up as the key itself; the drift tests check only literal keys, error codes and nav keys
    public string Translate(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var text = _strings.TryGetValue(key, out var found) ? found : key;

        return parameters is null ? text : Fill(text, parameters);
    }

    // unknown code -> generic message. The server's own message text is never shown.
    public string TranslateCode(string code, IReadOnlyDictionary<string, string>? parameters = null) =>
        Translate(Has(code) ? code : ErrorCodes.Unexpected, parameters);

    // the server sends every moment in UTC; a person reads it on their own clock, with the month in the app's language
    public string Date(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, _zone).ToString(Translate("format.date"), _culture);

    public string Time(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, _zone).ToString(Translate("format.time"), _culture);

    public string Offset(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, _zone).ToString(Translate("format.offset"), _culture);

    // midnight on the viewer's clock at the start of the last few calendar days, today included: 7 on a Monday starts on the Tuesday before
    public DateTimeOffset StartOfLastDays(DateTimeOffset now, int days)
    {
        var today = TimeZoneInfo.ConvertTime(now, _zone).Date;
        var first = today.AddDays(1 - days);

        return new DateTimeOffset(first, _zone.GetUtcOffset(first));
    }

    // a count with the thousands mark of the app's language, such as 1,284 in English and 1.284 in German
    public string Number(int value) => value.ToString("N0", _culture);

    // for XAML bindings, can't bind to a method
    public string this[string key] => Translate(key);

    public static Localizer Load(LanguageOption language) => Load(language, TimeZoneInfo.Local);

    // a test names the zone, so the text it expects is the same on every machine
    public static Localizer Load(LanguageOption language, TimeZoneInfo zone) =>
        new(language, StringsFrom(LanguageCatalog.ResourceNameFor(language.Code)), zone);

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

    // Must not throw either: a system without culture data still shows its dates, with English month names.
    private static CultureInfo CultureOf(LanguageOption language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language.Code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
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
