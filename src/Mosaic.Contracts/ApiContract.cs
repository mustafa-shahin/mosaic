namespace Mosaic.Contracts;

// The wire contract's version, not the build's. Major moves on a break, minor on an addition.
public static class ApiContract
{
    public const string Version = "1.0";

    private const int Unversioned = 0;

    public static bool IsCompatible(string? served) => MajorOf(served) == MajorOf(Version);

    private static int MajorOf(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Unversioned;
        }

        var separator = version.IndexOf('.');
        var major = separator < 0 ? version : version[..separator];

        return int.TryParse(major, out var parsed) ? parsed : Unversioned;
    }
}
