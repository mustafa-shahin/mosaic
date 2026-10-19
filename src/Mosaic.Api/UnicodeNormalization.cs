using System.Text;

namespace Mosaic.Api;

internal static class UnicodeNormalization
{
    // u followed by the two dots as a separate character, NFC joins them into one
    private const string Decomposed = "u\u0308";

    // in invariant globalization mode Normalize returns its input unchanged and throws nothing
    public static void EnsureAvailable()
    {
        if (Decomposed.Normalize(NormalizationForm.FormC).Length != 1)
        {
            throw new InvalidOperationException(
                "Unicode normalization does not work because invariant globalization mode is on "
                + "(DOTNET_SYSTEM_GLOBALIZATION_INVARIANT or InvariantGlobalization). Passwords would be "
                + "hashed, measured and checked against the blocklist without NFC. Run on an image that ships ICU.");
        }
    }
}
