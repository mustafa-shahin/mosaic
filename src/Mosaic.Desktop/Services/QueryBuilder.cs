using System.Globalization;

namespace Mosaic.Desktop.Services;

// the text after the path of an address, such as "?page=2&succeeded=false"
public sealed class QueryBuilder
{
    private readonly List<string> pairs = [];

    public QueryBuilder Add(string name, int value) =>
        Add(name, value.ToString(CultureInfo.InvariantCulture));

    public QueryBuilder Add(string name, Guid? value) =>
        Add(name, value?.ToString());

    // the round-trip form, 2026-11-17T00:00:00.0000000+01:00, which keeps the offset
    public QueryBuilder Add(string name, DateTimeOffset? value) =>
        Add(name, value?.ToString("O", CultureInfo.InvariantCulture));

    // lower case, as the JSON writes it; bool.ToString writes "True"
    public QueryBuilder Add(string name, bool? value) =>
        Add(name, value switch { true => "true", false => "false", null => null });

    // a value that is not set is left out, because the server refuses "succeeded=" with nothing after it
    public QueryBuilder Add(string name, string? value)
    {
        if (value is not null)
        {
            pairs.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return this;
    }

    public override string ToString() => pairs.Count == 0 ? string.Empty : "?" + string.Join('&', pairs);
}
