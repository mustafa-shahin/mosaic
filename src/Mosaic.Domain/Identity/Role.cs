using Mosaic.Domain.Authorization;
using Mosaic.Domain.Common;

namespace Mosaic.Domain.Identity;

public sealed class Role : SoftDeletableEntity
{
    private readonly List<Permission> _permissions = [];

    private Role()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public IReadOnlyList<Permission> Permissions => _permissions;

    // Set only on roles that pro seeds. Reseeding finds its rows by this key rather than by name, so an
    // owner-made role that happens to share the name is left alone.
    public string? SeedKey { get; private set; }

    public static Role Create(
        string name,
        string? description,
        IEnumerable<Permission> permissions,
        DateTimeOffset now)
    {
        var role = new Role
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = now,
        };

        role.SetPermissions(permissions);

        return role;
    }

    public static Role Seeded(
        string seedKey,
        string name,
        string? description,
        IEnumerable<Permission> permissions,
        DateTimeOffset now)
    {
        var role = Create(name, description, permissions, now);

        role.SeedKey = seedKey;

        return role;
    }

    public void Rename(string name, string? description)
    {
        Name = name.Trim();
        Description = description;
    }

    public void SetPermissions(IEnumerable<Permission> permissions)
    {
        _permissions.Clear();
        _permissions.AddRange(permissions.Distinct());
    }
}
