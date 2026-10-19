namespace Mosaic.Desktop.Shell.SignIn;

// no session exists, so the sign-in screen goes on from here with the address, a note and the reason
public sealed record SignInHandover(
    string Email, SignInNote? Note, string? ErrorCode = null, IReadOnlyDictionary<string, string>? ErrorParams = null);
