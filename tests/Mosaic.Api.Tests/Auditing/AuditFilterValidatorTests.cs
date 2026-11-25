using FluentValidation.Results;
using Mosaic.Api.Auditing;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;

namespace Mosaic.Api.Tests.Auditing;

public sealed class AuditFilterValidatorTests
{
    private static ValidationResult Validate(string? action) =>
        new AuditFilterValidator().Validate(new AuditFilter { Action = action });

    [Theory]
    [InlineData(null)]
    [InlineData(AuditActions.SignIn)]
    [InlineData("no.such.action")]
    public void NoActionOrANamedOne_IsValid(string? action)
    {
        Assert.True(Validate(action).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyAction_IsAnInvalidValue(string action)
    {
        var failure = Assert.Single(Validate(action).Errors);

        Assert.Equal(nameof(AuditFilter.Action), failure.PropertyName);
        Assert.Equal(ErrorCodes.InvalidValue, failure.ErrorCode);
    }
}
