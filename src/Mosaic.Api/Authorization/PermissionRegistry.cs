using Mosaic.Contracts;
using Mosaic.Domain.Authorization;
using Mosaic.Domain.Errors;

namespace Mosaic.Api.Authorization;

// Every permission key this build knows about. Each edition registers its own at startup, core first.
public sealed class PermissionRegistry
{
    private readonly Dictionary<string, Permission> _byKey = new(StringComparer.Ordinal);

    // Throws on a key registered twice, otherwise a key already stored in a role row would change meaning.
    public PermissionRegistry(IEnumerable<IPermissionSource> sources)
    {
        foreach (var source in sources)
        {
            foreach (var permission in source.Permissions)
            {
                if (!_byKey.TryAdd(permission.Key, permission))
                {
                    throw new InvalidOperationException($"Permission key '{permission.Key}' is registered twice.");
                }
            }
        }
    }

    public IReadOnlyCollection<Permission> All => _byKey.Values;

    public bool IsRegistered(string key) => _byKey.ContainsKey(key);

    // For a key from a request or an attribute, where an unknown key is an error, and never for a key
    // read back from the database, see PermissionListConverter.
    public Permission Parse(string key) =>
        _byKey.TryGetValue(key, out var permission)
            ? permission
            : throw new InvalidRequestException(ErrorCodes.UnknownPermission, $"'{key}' is not registered.");
}

