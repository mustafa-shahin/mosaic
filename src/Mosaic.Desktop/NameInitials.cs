using System.Globalization;

namespace Mosaic.Desktop;

// the first letter of the first word and of the last word, for the round badge before a person's name: Bruce Wayne => BW, Clark Joseph Kent => CK, Diana => D
public static class NameInitials
{
    public static string Of(string? fullName)
    {
        var words = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => string.Empty,
            1 => FirstLetter(words[0]),
            _ => FirstLetter(words[0]) + FirstLetter(words[^1]),
        };
    }

    // a letter is a whole text element, so an accent written as a second character stays on it
    private static string FirstLetter(string word) => StringInfo.GetNextTextElement(word).ToUpperInvariant();
}
