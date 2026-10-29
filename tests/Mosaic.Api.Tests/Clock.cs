namespace Mosaic.Api.Tests;

// a clock the test moves by hand, for the rules that depend on how much time has passed
internal sealed class Clock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
