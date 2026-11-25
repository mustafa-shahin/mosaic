using FluentValidation.Results;
using Mosaic.Api.Validation;
using Mosaic.Contracts;

namespace Mosaic.Api.Tests.Validation;

public sealed class PageRequestValidatorTests
{
    private static ValidationResult Validate(int page, int pageSize) =>
        new PageRequestValidator().Validate(new PageRequest { Page = page, PageSize = pageSize });

    [Theory]
    [InlineData(Paging.FirstPage, Paging.MinPageSize)]
    [InlineData(Paging.FirstPage, Paging.MaxPageSize)]
    [InlineData(Paging.MaxPage, Paging.MaxPageSize)]
    public void APageAndASizeWithinTheLimits_AreValid(int page, int pageSize)
    {
        Assert.True(Validate(page, pageSize).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APageBeforeTheFirst_IsTooSmall(int page)
    {
        var failure = Assert.Single(Validate(page, Paging.DefaultPageSize).Errors);

        Assert.Equal(nameof(PageRequest.Page), failure.PropertyName);
        Assert.Equal(ErrorCodes.TooSmall, failure.ErrorCode);
    }

    [Theory]
    [InlineData(Paging.MaxPage + 1)]
    [InlineData(int.MaxValue)]
    public void APageAfterTheLargest_IsTooLarge(int page)
    {
        var failure = Assert.Single(Validate(page, Paging.DefaultPageSize).Errors);

        Assert.Equal(nameof(PageRequest.Page), failure.PropertyName);
        Assert.Equal(ErrorCodes.TooLarge, failure.ErrorCode);
    }

    [Theory]
    [InlineData(0, ErrorCodes.TooSmall)]
    [InlineData(-5, ErrorCodes.TooSmall)]
    [InlineData(Paging.MaxPageSize + 1, ErrorCodes.TooLarge)]
    public void ASizeOutsideTheLimits_IsRefusedWithItsCode(int pageSize, string code)
    {
        var failure = Assert.Single(Validate(Paging.FirstPage, pageSize).Errors);

        Assert.Equal(nameof(PageRequest.PageSize), failure.PropertyName);
        Assert.Equal(code, failure.ErrorCode);
    }

    [Fact]
    public void TwoWrongFields_AreBothReported()
    {
        var failures = Validate(0, Paging.MaxPageSize + 1).Errors;

        Assert.Equal("Page PageSize", string.Join(' ', failures.Select(failure => failure.PropertyName)));
    }
}
