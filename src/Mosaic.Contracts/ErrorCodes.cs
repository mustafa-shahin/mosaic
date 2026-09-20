namespace Mosaic.Contracts;

// Don't rename or remove codes once shipped. Older desktops translate them by string.
public static class ErrorCodes
{
    public const string InvalidEmail = "error.invalid_email";
    public const string InvalidValue = "error.invalid_value";
    public const string Required = "error.required";
    public const string TooLong = "error.too_long";
    public const string TooShort = "error.too_short";
    public const string Unexpected = "error.unexpected";
    public const string UnknownPermission = "error.unknown_permission";
    public const string ValidationFailed = "error.validation_failed";
}
