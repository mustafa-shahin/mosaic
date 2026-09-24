namespace Mosaic.Domain.Settings;

// stored as its name, so renaming a member makes the next migration fail on rows that hold the old one
public enum RegistrationPolicy
{
    // first, so a value nobody set is the safe one
    Closed,
    Open,
}
