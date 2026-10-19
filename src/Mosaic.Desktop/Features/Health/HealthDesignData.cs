using Mosaic.Desktop.Services.Health;

namespace Mosaic.Desktop.Features.Health;

// For the previewer only.
internal static class HealthDesignData
{
    public static HealthViewModel Reachable { get; } =
        new(new HealthApi(DesignContext.Api), DesignContext.Strings)
        {
            State = ConnectionState.Reachable,
            Service = "mosaic-api",
            ServerTimeUtc = new DateTimeOffset(2026, 9, 12, 9, 41, 0, TimeSpan.Zero)
        };
}
