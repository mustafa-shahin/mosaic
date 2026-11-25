namespace Mosaic.Contracts.Auditing;

// an audit row saves one of these values in its target_type column, so never change a value later:
// rows saved before the change keep the old value, and a filter for the new value does not find them
public static class AuditTargets
{
    public const string User = "core.user";
}
