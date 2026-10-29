namespace Mosaic.Contracts.Identity;

// The auth endpoints' segments, which the desktop's AuthApi builds its paths from.
public static class AuthRoutes
{
    public const string Prefix = "auth";
    public const string Rules = "rules";
    public const string Register = "register";
    public const string SignIn = "sign-in";
    public const string Session = "session";
    public const string SignOut = "sign-out";
    public const string SignOutEverywhere = "sign-out-everywhere";
}
