using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Tests.Services;

public sealed class QueryBuilderTests
{
    [Fact]
    public void NoValue_GivesNoText()
    {
        Assert.Equal(string.Empty, new QueryBuilder().ToString());
    }

    [Fact]
    public void TheValues_FollowTheQuestionMarkInTheOrderTheyWereAdded()
    {
        var query = new QueryBuilder().Add("page", 2).Add("action", "core.auth.sign_in").Add("succeeded", true);

        Assert.Equal("?page=2&action=core.auth.sign_in&succeeded=true", query.ToString());
    }

    // the server reads the lower case words; bool.ToString would write False
    [Fact]
    public void ABool_IsWrittenInLowerCase()
    {
        Assert.Equal("?succeeded=false", new QueryBuilder().Add("succeeded", false).ToString());
    }

    [Fact]
    public void AValueThatIsNotSet_IsLeftOut()
    {
        var query = new QueryBuilder()
            .Add("succeeded", (bool?)null)
            .Add("action", (string?)null)
            .Add("person", (Guid?)null)
            .Add("from", (DateTimeOffset?)null);

        Assert.Equal(string.Empty, query.ToString());
    }

    [Fact]
    public void AGuid_IsWrittenWithItsHyphens()
    {
        var person = Guid.Parse("01a1dbd1-60a4-727b-bacd-c080468be164");

        Assert.Equal("?person=01a1dbd1-60a4-727b-bacd-c080468be164", new QueryBuilder().Add("person", person).ToString());
    }

    // a + left as it is would reach the server as a space, and the offset would be lost
    [Fact]
    public void AMoment_KeepsItsOffset_Escaped()
    {
        var midnightInBerlin = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(
            "?from=2026-11-17T00%3A00%3A00.0000000%2B01%3A00",
            new QueryBuilder().Add("from", midnightInBerlin).ToString());
    }

    [Fact]
    public void AText_IsEscaped()
    {
        Assert.Equal("?search=bruce%20w%26m%3D1", new QueryBuilder().Add("search", "bruce w&m=1").ToString());
    }
}
