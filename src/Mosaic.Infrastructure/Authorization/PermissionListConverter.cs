using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mosaic.Domain.Authorization;

namespace Mosaic.Infrastructure.Authorization;

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

    // A key this build does not declare is kept as it is, not dropped. Pro's keys sit in role rows
    // while a licence is lapsed, and dropping them would delete them for good the first time an owner
    // saved that role. Nothing can act on one, because no check asks for a key that is not declared.
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
