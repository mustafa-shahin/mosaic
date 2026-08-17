namespace Mosaic.Contracts;

public sealed record HealthResult
{
    public required string Service { get; init; }
    public required DateTimeOffset ServerTimeUtc { get; init; }

    // Not required: an older server sends nothing, and required would throw on exactly that response.
    public string? ApiContract { get; init; }
}