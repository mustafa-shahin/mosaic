namespace Mosaic.Contracts;

public sealed record HealthResult
{
    public required string Service { get; init; }
    public required DateTimeOffset ServerTimeUtc { get; init; }
}