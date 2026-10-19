namespace Mosaic.Desktop.Tests;

// What POST /v1/auth/register answers, as the API writes it except the trace id every refusal carries.
internal static class RegisterBodies
{
    public const string Owner = """{"success":true}""";

    public const string SetupTokenRequired = """{"error":{"code":"error.setup_token_required"},"success":false}""";

    public const string SetupTokenInvalid = """{"error":{"code":"error.setup_token_invalid"},"success":false}""";

    public const string RegistrationClosed = """{"error":{"code":"error.registration_closed"},"success":false}""";

    // one code per field, as the validator sends it
    public const string InvalidEmailAndTooShort =
        """{"error":{"code":"error.validation_failed","fieldErrors":{"email":[{"code":"error.invalid_email"}],"password":[{"code":"error.too_short","params":{"limit":"15"}}]}},"success":false}""";

    public const string UnknownField =
        """{"error":{"code":"error.validation_failed","fieldErrors":{"timeZone":[{"code":"error.required"}]}},"success":false}""";
}
