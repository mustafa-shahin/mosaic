namespace Mosaic.Domain.Authorization;

// Not an enum, because pro adds keys of its own and core builds without pro. The key is what is
// stored in the database, so renaming one needs a migration. The catalogue is not here: no domain
// rule names a key, so CorePermissions lives in Application next to the registry.
public sealed record Permission(string Key)
{
    public override string ToString() => Key;
}
