using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Tests;

public sealed class ApiEndpointTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenNothingIsConfigured_UsesTheDevelopmentDefault(string? configured)
    {
        Assert.Equal(new Uri("http://localhost:5175/"), ApiEndpoint.Resolve(configured));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("http://")]
    [InlineData("//localhost:5175")]
    [InlineData("localhost:5175")]
    [InlineData("ftp://example.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://localhost:5175?tenant=1")]
    public void Resolve_WhenTheValueCannotBeUsed_ReturnsNull(string configured)
    {
        Assert.Null(ApiEndpoint.Resolve(configured));
    }

    [Theory]
    [InlineData("http://localhost:5175", "http://localhost:5175/")]
    [InlineData(" http://localhost:5175 ", "http://localhost:5175/")]
    [InlineData("HTTP://LOCALHOST:5175", "http://localhost:5175/")]
    [InlineData("https://mosaic.example.com/api", "https://mosaic.example.com/api/")]
    [InlineData("https://mosaic.example.com/api/", "https://mosaic.example.com/api/")]
    public void Resolve_WhenTheValueIsUsable_NormalisesItWithATrailingSlash(string configured, string expected)
    {
        Assert.Equal(new Uri(expected), ApiEndpoint.Resolve(configured));
    }
}