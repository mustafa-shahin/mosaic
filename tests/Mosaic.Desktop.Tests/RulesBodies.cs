namespace Mosaic.Desktop.Tests;

// What GET /v1/auth/rules answers, written the way the API writes it.
internal static class RulesBodies
{
    public const string Unclaimed =
        """{"data":{"registration":"Unclaimed","password":{"minLength":15,"maxLength":128}},"success":true}""";

    public const string Closed =
        """{"data":{"registration":"Closed","password":{"minLength":15,"maxLength":128}},"success":true}""";
}
