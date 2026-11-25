namespace Mosaic.Contracts.Auditing;

// which audit rows a caller wants. A value that is not set leaves the list as it is.
public sealed record AuditFilter
{
    public const string SucceededParameter = "succeeded";
    public const string ActionParameter = "action";
    public const string PersonParameter = "person";
    public const string FromParameter = "from";

    public bool? Succeeded { get; init; }

    public string? Action { get; init; }

    // the rows this person did and the rows done to them
    public Guid? Person { get; init; }

    // the rows from this moment on
    public DateTimeOffset? From { get; init; }
}
