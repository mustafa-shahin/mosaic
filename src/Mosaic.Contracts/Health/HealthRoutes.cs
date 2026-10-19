namespace Mosaic.Contracts.Health;

public static class HealthRoutes
{
    // no version, so a monitor and an older desktop reach it whatever version the server speaks
    public const string Health = "health";
}
