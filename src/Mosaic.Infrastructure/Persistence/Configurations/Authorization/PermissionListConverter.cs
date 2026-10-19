using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mosaic.Domain.Authorization;

namespace Mosaic.Infrastructure.Persistence.Configurations.Authorization;

internal static class PermissionListConverter
{
    public static readonly ValueConverter<IReadOnlyList<Permission>, string[]> Converter = new(
        permissions => permissions.Select(permission => permission.Key).ToArray(),
        keys => Read(keys));

    // Change tracking snapshots a reference by default, so mutating the list in place would compare
    // equal to itself and the update would never be written.
    public static readonly ValueComparer<IReadOnlyList<Permission>> Comparer = new(
        (left, right) => left!.SequenceEqual(right!),
        permissions => permissions.Aggregate(0, (hash, permission) => HashCode.Combine(hash, permission)),
        permissions => permissions.ToList());

    // A key this build does not declare is kept, because no check asks for it and dropping it would erase it
    // from the row the next time the role's permissions change.
    private static List<Permission> Read(string[] keys)
    {
        var permissions = new List<Permission>(keys.Length);

        foreach (var key in keys)
        {
            permissions.Add(new Permission(key));
        }

        return permissions;
    }
}
