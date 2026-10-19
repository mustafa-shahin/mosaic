namespace Mosaic.Contracts.Identity;

// who signed in, with no owner flag or permission, so the desktop never decides what a person may see
public sealed record SessionResult
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
}
