using System.Collections.Frozen;
using System.Text;

namespace Mosaic.Application.Identity;

// the blocklist: breached passwords compiled into the dll, plus the product name and the account's own words
internal static class CommonPasswords
{
    // the product's name, known to anyone who attacks a Mosaic installation
    private const string ProductName = "mosaic";

    // the LogicalName given to the list in Mosaic.Application.csproj
    private const string ResourceName = "Mosaic.Application.Identity.common-passwords.txt";

    // Lazy calls Load on the first read of Value, a second caller during the load waits for the same set
    private static readonly Lazy<FrozenSet<string>> Entries = new(Load);

    public static bool IsCommon(string password, string? email, string? firstName, string? lastName)
    {
        var folded = Fold(password);

        // the whole password is looked up, never the words inside it, so a passphrase is not refused for one common word
        if (Entries.Value.Contains(folded))
        {
            return true;
        }

        // "adalovelace1815!" becomes "adalovelace"
        var core = Core(folded);

        // a password of only digits and symbols has an empty core, which a missing name must not match
        return core.Length > 0
            && ContextWords(email, firstName, lastName).Any(word => Core(Fold(word)) == core);
    }

    // values an attacker can find out before guessing
    private static string[] ContextWords(string? email, string? firstName, string? lastName)
    {
        var address = email ?? "";
        var first = firstName ?? "";
        var last = lastName ?? "";

        // the last @, a quoted local part may contain one too
        var at = address.LastIndexOf('@');

        return
        [
            ProductName,
            address,
            at > 0 ? address[..at] : "",
            first,
            last,
            // joined in both orders, with and without a space, the way people type them
            first + last,
            last + first,
            $"{first} {last}",
            $"{last} {first}",
        ];
    }

    // the list was built with the same two steps, so it only matches text folded this way
    private static string Fold(string text) => text.Normalize(NormalizationForm.FormC).ToLowerInvariant();

    // digits, symbols and spaces around a word do not hide it, "Mosaic2026!" is still "mosaic"
    private static string Core(string text)
    {
        // start is the first character kept and end is one past the last, so at first the whole text is kept
        var start = 0;
        var end = text.Length;

        // skip what is not a letter at the front, "!!mosaic" starts at the m
        while (start < end && !char.IsLetter(text[start]))
        {
            start++;
        }

        // the same at the back, text[end - 1] is the last character still kept
        while (end > start && !char.IsLetter(text[end - 1]))
        {
            end--;
        }

        // the characters from start up to end, "mosaic2026!" gives "mosaic" and "2026!" gives an empty string
        return text[start..end];
    }

    // the list lives inside this dll, so nothing is read from disk
    private static FrozenSet<string> Load()
    {
        // an empty set would let every password through, so a missing resource throws
        using var stream = typeof(CommonPasswords).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);

        var entries = new List<string>();

        // ReadLine accepts Windows and Unix line ends, git checks the list out with either
        while (reader.ReadLine() is { } line)
        {
            entries.Add(line);
        }

        // a FrozenSet cannot change after it is built and is optimised for lookups
        return entries.ToFrozenSet(StringComparer.Ordinal);
    }
}
