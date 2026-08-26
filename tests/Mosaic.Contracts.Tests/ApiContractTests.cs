namespace Mosaic.Contracts.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void Version_IsMajorDotMinorWithAMajorAboveZero()
    {
        var parts = ApiContract.Version.Split('.');

        Assert.Equal(2, parts.Length);
        Assert.True(int.TryParse(parts[0], out var major));
        Assert.True(major > 0);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.4")]
    [InlineData("1.99")]
    public void SameMajor_IsCompatible_WhateverTheMinor(string served)
    {
        Assert.True(ApiContract.IsCompatible(served));
    }

    [Theory]
    [InlineData("0.9")]
    [InlineData("2.0")]
    public void DifferentMajor_IsNotCompatible(string served)
    {
        Assert.False(ApiContract.IsCompatible(served));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    public void MissingOrUnreadable_IsNotCompatible(string? served)
    {
        Assert.False(ApiContract.IsCompatible(served));
    }
}
