namespace Mosaic.Desktop.Features.Health;

// For the previewer only. Named by state, so an unreachable sample can sit beside this one later.
internal static class HealthDesignData
{
    public static HealthViewModel Reachable { get; } =
        new(DesignContext.Api, DesignContext.Strings)
        {
            State = ConnectionState.Reachable,
            Service = "mosaic-api",
            ServerTimeUtc = new DateTimeOffset(2026, 9, 12, 9, 41, 0, TimeSpan.Zero)
        };
}
