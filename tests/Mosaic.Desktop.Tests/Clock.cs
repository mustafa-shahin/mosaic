namespace Mosaic.Desktop.Tests;

// a clock that always says the same moment, so a date range starts on the same day on every machine
internal sealed class Clock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
