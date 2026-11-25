namespace Mosaic.Contracts.Auditing;

// the values the filters of the audit list can offer, taken from the audit rows themselves
public sealed record AuditFilterChoices
{
    public required IReadOnlyList<string> Actions { get; init; }
}
