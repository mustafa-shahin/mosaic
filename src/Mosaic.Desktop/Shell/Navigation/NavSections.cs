namespace Mosaic.Desktop.Shell.Navigation;

// Section keys are never persisted and never translated. They only join a contribution to a heading,
// and being constants means a typo cannot quietly create a second section. Pro declares its own.
public static class NavSections
{
    public const string Overview = "overview";
    public const string Administration = "administration";
}