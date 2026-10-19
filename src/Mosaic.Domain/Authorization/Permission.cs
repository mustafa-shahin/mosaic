namespace Mosaic.Domain.Authorization;

// Not an enum, pro adds its own keys; the key is stored in role rows, so renaming one needs a migration.
public sealed record Permission(string Key)
{
    public override string ToString() => Key;
}
