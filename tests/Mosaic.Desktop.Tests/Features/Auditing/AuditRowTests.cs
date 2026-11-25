using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Desktop.Features.Auditing;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Tests.Features.Auditing;

public sealed class AuditRowTests
{
    private static readonly Localizer English =
        Localizer.Load(LanguageCatalog.Resolve("en"), TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

    private static readonly AuditEntryResult SignIn = new()
    {
        Id = Guid.CreateVersion7(),
        OccurredAt = new DateTimeOffset(2026, 11, 25, 9, 41, 12, TimeSpan.Zero),
        Action = AuditActions.SignIn,
        Succeeded = true,
        ActorName = "Bruce Wayne",
        ActorEmail = "bruce.wayne@example.com",
        TargetType = AuditTargets.User,
        TargetName = "Bruce Wayne",
        CorrelationId = "35959a94f3577b34da6a3ce929d0e0e4",
        IPAddress = "203.0.113.7",
    };

    // a wrong password on an account: nobody proved who they are
    private static readonly AuditEntryResult WrongPassword = SignIn with
    {
        Succeeded = false,
        ErrorCode = ErrorCodes.InvalidCredentials,
        ActorName = null,
        ActorEmail = null,
    };

    [Fact]
    public void ARow_ShowsTheMomentOnTheViewersClock()
    {
        var row = AuditRow.From(SignIn, English);

        Assert.Equal("25 Nov 2026", row.Date);
        Assert.Equal("10:41:12", row.Time);
        Assert.Equal("UTC+01:00", row.Offset);
    }

    [Fact]
    public void ARow_ShowsTheActionTheOutcomeAndTheKindInWords()
    {
        var row = AuditRow.From(SignIn, English);

        Assert.Equal("Sign-in", row.Action);
        Assert.True(row.Succeeded);
        Assert.Equal("Succeeded", row.Outcome);
        Assert.Null(row.Reason);
        Assert.Equal("User", row.Kind);
    }

    [Fact]
    public void ARow_CarriesThePersonTheTargetAndTheTrace()
    {
        var row = AuditRow.From(SignIn, English);

        Assert.Equal("Bruce Wayne", row.Person);
        Assert.Equal("BW", row.Initials);
        Assert.False(row.IsAnonymous);
        Assert.Equal("bruce.wayne@example.com", row.Email);
        Assert.Equal("Bruce Wayne", row.Target);
        Assert.Equal("203.0.113.7", row.IPAddress);
        Assert.Equal("35959a94f3577b34da6a3ce929d0e0e4", row.TraceId);
    }

    [Fact]
    public void AFailedRow_ShowsItsReasonInWords()
    {
        var row = AuditRow.From(WrongPassword, English);

        Assert.False(row.Succeeded);
        Assert.Equal("Failed", row.Outcome);
        Assert.Equal("Wrong email or password", row.Reason);
    }

    // a newer server may save a code this desktop has no words for yet
    [Fact]
    public void AReasonWithoutWords_IsShownAsItsCode()
    {
        var row = AuditRow.From(WrongPassword with { ErrorCode = "error.something_newer" }, English);

        Assert.Equal("error.something_newer", row.Reason);
    }

    [Fact]
    public void ARowWithNoActor_ShowsAnonymous_WithAnEmptyBadge()
    {
        var row = AuditRow.From(WrongPassword, English);

        Assert.Equal("Anonymous", row.Person);
        Assert.True(row.IsAnonymous);
        Assert.Equal(string.Empty, row.Initials);
        Assert.Null(row.Email);
    }

    [Fact]
    public void ARowWithNoTarget_ShowsADash_AndNoKind()
    {
        var row = AuditRow.From(SignIn with { TargetType = null, TargetName = null }, English);

        Assert.Equal("—", row.Target);
        Assert.Null(row.Kind);
    }

    // 23:30 in UTC is already the next day in Berlin
    [Fact]
    public void ListFrom_MarksTheFirstRowOfEachDayOnTheViewersClock()
    {
        IReadOnlyList<AuditEntryResult> entries =
        [
            SignIn with { OccurredAt = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero) },
            SignIn with { OccurredAt = new DateTimeOffset(2026, 11, 24, 23, 30, 0, TimeSpan.Zero) },
            SignIn with { OccurredAt = new DateTimeOffset(2026, 11, 24, 22, 30, 0, TimeSpan.Zero) },
            SignIn with { OccurredAt = new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero) },
        ];

        var rows = AuditRow.ListFrom(entries, English);

        Assert.Equal([true, false, true, true], rows.Select(row => row.StartsDay));
    }
}
