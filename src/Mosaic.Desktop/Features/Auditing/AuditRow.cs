using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Features.Auditing;

// one entry as the screen shows it, with every word already chosen for the person who reads it
public sealed record AuditRow
{
    private const string NoTarget = "—";

    // the short text beside a failed outcome, found by the code that the server saved with the row
    private static readonly Dictionary<string, string> ReasonKeys = new()
    {
        [ErrorCodes.AccountInactive] = "audit.reason.account_inactive",
        [ErrorCodes.AccountLocked] = "audit.reason.account_locked",
        [ErrorCodes.InvalidCredentials] = "audit.reason.invalid_credentials",
        [ErrorCodes.RegistrationClosed] = "audit.reason.registration_closed",
        [ErrorCodes.SetupTokenInvalid] = "audit.reason.setup_token_invalid",
        [ErrorCodes.SetupTokenRequired] = "audit.reason.setup_token_required",
        [ErrorCodes.ValidationFailed] = "audit.reason.validation_failed",
    };

    public required string Date { get; init; }
    public required string Time { get; init; }
    public required string Offset { get; init; }
    public required string Action { get; init; }
    public required bool Succeeded { get; init; }
    public required string Outcome { get; init; }
    public string? Reason { get; init; }
    public required string Person { get; init; }
    public required string Initials { get; init; }
    public required bool IsAnonymous { get; init; }
    public string? Email { get; init; }
    public required string Target { get; init; }
    public string? Kind { get; init; }
    public string? IPAddress { get; init; }
    public required string TraceId { get; init; }

    // true on the first row of a day, so the screen can show a date brightly once and quietly below it
    public bool StartsDay { get; init; }

    public static IReadOnlyList<AuditRow> ListFrom(IReadOnlyList<AuditEntryResult> entries, Localizer strings)
    {
        var rows = new List<AuditRow>();
        string? previous = null;

        foreach (var entry in entries)
        {
            var row = From(entry, strings);

            rows.Add(row with { StartsDay = row.Date != previous });
            previous = row.Date;
        }

        return rows;
    }

    public static AuditRow From(AuditEntryResult entry, Localizer strings) => new()
    {
        Date = strings.Date(entry.OccurredAt),
        Time = strings.Time(entry.OccurredAt),
        Offset = strings.Offset(entry.OccurredAt),
        Action = strings.Translate(entry.Action),
        Succeeded = entry.Succeeded,
        Outcome = entry.Succeeded ? strings.Translate("audit.succeeded") : strings.Translate("audit.failed"),
        Reason = ReasonOf(entry.ErrorCode, strings),
        Person = entry.ActorName ?? strings.Translate("audit.anonymous"),
        Initials = NameInitials.Of(entry.ActorName),
        IsAnonymous = entry.ActorName is null,
        Email = entry.ActorEmail,
        Kind = entry.TargetType is null ? null : strings.Translate(entry.TargetType),
        IPAddress = entry.IPAddress,
        TraceId = entry.CorrelationId,
        Target = entry.TargetName ?? NoTarget,
    };

    // a code with no short text here is shown as it is, so the reason of a newer server is never hidden
    private static string? ReasonOf(string? errorCode, Localizer strings)
    {
        if (errorCode is null)
        {
            return null;
        }

        return ReasonKeys.TryGetValue(errorCode, out var key) ? strings.Translate(key) : errorCode;
    }
}
