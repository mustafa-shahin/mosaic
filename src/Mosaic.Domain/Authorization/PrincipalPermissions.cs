namespace Mosaic.Domain.Authorization;

public sealed class PrincipalPermissions
{
    private readonly HashSet<Permission> _granted;

    private PrincipalPermissions(bool isOwner, IEnumerable<Permission> granted)
    {
        IsOwner = isOwner;
        _granted = [.. granted];
    }

    public bool IsOwner { get; }

    public IReadOnlyCollection<Permission> Granted => _granted;

    // The owner passes every check, including keys that only pro declares.
    public bool Has(Permission permission) => IsOwner || _granted.Contains(permission);

    public static PrincipalPermissions ForUser(bool isOwner, IEnumerable<Permission> roleGrants)
        => new(isOwner, roleGrants);
}
