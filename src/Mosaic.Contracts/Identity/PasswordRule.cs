namespace Mosaic.Contracts.Identity;

// counted in code points after NFC, so a string's Length is not the same number
public sealed record PasswordRule
{
    public required int MinLength { get; init; }
    public required int MaxLength { get; init; }
}
