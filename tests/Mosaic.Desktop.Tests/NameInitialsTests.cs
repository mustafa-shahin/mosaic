namespace Mosaic.Desktop.Tests;

public sealed class NameInitialsTests
{
    // E followed by U+0301, the accent as its own character
    [Theory]
    [InlineData("Bruce Wayne", "BW")]
    [InlineData("Clark Joseph Kent", "CK")]
    [InlineData("Diana", "D")]
    [InlineData("bruce wayne", "BW")]
    [InlineData("  Bruce   Wayne  ", "BW")]
    [InlineData("E\u0301lodie Martin", "E\u0301M")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Of_TakesTheFirstLetterOfTheFirstAndTheLastWord(string? fullName, string initials)
    {
        Assert.Equal(initials, NameInitials.Of(fullName));
    }
}
