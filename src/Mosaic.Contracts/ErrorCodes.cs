namespace Mosaic.Contracts;

// Don't rename or remove codes once shipped. Older desktops translate them by string.
public static class ErrorCodes
{
    public const string AccountInactive = "error.account_inactive";

    // audit rows only, the sign-in answer stays InvalidCredentials so a stranger cannot learn the address exists
    public const string AccountLocked = "error.account_locked";

    public const string AlreadyTaken = "error.already_taken";
    public const string CommonPassword = "error.common_password";
    public const string InvalidCredentials = "error.invalid_credentials";
    public const string InvalidEmail = "error.invalid_email";
    public const string InvalidValue = "error.invalid_value";
    public const string RegistrationClosed = "error.registration_closed";
    public const string Required = "error.required";
    public const string ServerBusy = "error.server_busy";
    public const string SetupTokenInvalid = "error.setup_token_invalid";
    public const string SetupTokenRequired = "error.setup_token_required";
    public const string TooLarge = "error.too_large";
    public const string TooLong = "error.too_long";
    public const string TooManyRequests = "error.too_many_requests";
    public const string TooShort = "error.too_short";
    public const string TooSmall = "error.too_small";
    public const string Unexpected = "error.unexpected";
    public const string UnknownPermission = "error.unknown_permission";
    public const string ValidationFailed = "error.validation_failed";
}
