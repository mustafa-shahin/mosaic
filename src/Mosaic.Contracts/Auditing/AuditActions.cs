namespace Mosaic.Contracts.Auditing;

// every audit row saves one of these values in its action column, so never change a value later:
// rows saved before the change keep the old value, and a filter for the new value does not find them
public static class AuditActions
{
    public const string Register = "core.auth.register";
    public const string SignIn = "core.auth.sign_in";
    public const string SignOut = "core.auth.sign_out";
    public const string SignOutEverywhere = "core.auth.sign_out_everywhere";
}
