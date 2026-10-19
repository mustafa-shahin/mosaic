namespace Mosaic.Desktop;

// Codes the client raises about itself. Server codes arrive in the envelope and are never declared here.
public static class DesktopErrorCodes
{
    public const string ApiUnreachable = "client.api_unreachable";
    public const string ApiTimedOut = "client.api_timed_out";
    public const string UnexpectedResponse = "client.unexpected_response";
    public const string ApiAddressInvalid = "client.api_address_invalid";
    public const string NotSignedIn = "client.not_signed_in";
    public const string NotAllowed = "client.not_allowed";
    public const string PasswordsDiffer = "client.passwords_differ";
}
