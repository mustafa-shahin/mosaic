namespace Mosaic.Desktop.Features.Auditing;

// one choice of the action filter: its text, and the code the request sends (null for every action)
public sealed record ActionOption(string Text, string? Action);

// one choice of the outcome switch: its text, and the value the request sends (null for every row)
public sealed record OutcomeOption(string Text, bool? Succeeded);

// one suggestion of the person box: what the person filter sends, and what the suggestion shows
public sealed record PersonOption(Guid Id, string Name, string Email, string Initials);

// one choice of the date range: its text, and how many calendar days back it reaches, today included (null for any time)
public sealed record DateRangeOption(string Text, int? Days);
