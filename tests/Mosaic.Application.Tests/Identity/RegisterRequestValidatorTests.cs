using FluentValidation.Results;
using Mosaic.Application.Identity;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Application.Tests.Identity;

public sealed class RegisterRequestValidatorTests
{
    private static readonly RegisterRequest Complete = new()
    {
        Email = "ada@example.com",
        FirstName = "Ada",
        LastName = "Lovelace",
        Password = "correct horse battery staple",
    };

    private static ValidationResult Validate(RegisterRequest request) => new RegisterRequestValidator().Validate(request);

    [Fact]
    public void ACompleteRequest_IsValid()
    {
        Assert.True(Validate(Complete).IsValid);
    }

    [Fact]
    public void AnEmptyRequest_IsMissingEveryFieldOnce()
    {
        var failures = Validate(new RegisterRequest()).Errors;

        Assert.Equal("Email FirstName LastName Password", string.Join(' ', failures.Select(failure => failure.PropertyName)));
        Assert.All(failures, failure => Assert.Equal(ErrorCodes.Required, failure.ErrorCode));
    }

    [Fact]
    public void OnlySpaces_CountAsMissing()
    {
        var failures = Validate(new RegisterRequest { Email = "   ", FirstName = " ", LastName = "  ", Password = "     " }).Errors;

        Assert.Equal(4, failures.Count);
        Assert.All(failures, failure => Assert.Equal(ErrorCodes.Required, failure.ErrorCode));
    }

    [Theory]
    [InlineData("Ada <ada@example.com>")]
    [InlineData("a@b@example.com")]
    [InlineData("ada.example.com")]
    public void NotASingleAddress_IsInvalid(string email)
    {
        var failure = Assert.Single(Validate(Complete with { Email = email }).Errors);

        Assert.Equal(nameof(RegisterRequest.Email), failure.PropertyName);
        Assert.Equal(ErrorCodes.InvalidEmail, failure.ErrorCode);
    }

    // User.Normalize lowercases the address when the user is created
    [Fact]
    public void AnAddressInCapitals_IsValid()
    {
        Assert.True(Validate(Complete with { Email = "Ada@Example.COM" }).IsValid);
    }

    // too long and not a single address either, but only the first failure is reported
    [Fact]
    public void AnAddressPastTheLimit_IsTooLongOnly()
    {
        var email = "Ada <" + new string('a', UserLimits.EmailLength) + "@example.com>";

        var failure = Assert.Single(Validate(Complete with { Email = email }).Errors);

        Assert.Equal(ErrorCodes.TooLong, failure.ErrorCode);
    }

    [Theory]
    [InlineData(UserLimits.NameLength, true)]
    [InlineData(UserLimits.NameLength + 1, false)]
    public void AFirstName_MayBeAsLongAsTheLimit(int length, bool valid)
    {
        Assert.Equal(valid, Validate(Complete with { FirstName = new string('a', length) }).IsValid);
    }

    [Theory]
    [InlineData(UserLimits.NameLength, true)]
    [InlineData(UserLimits.NameLength + 1, false)]
    public void ALastName_MayBeAsLongAsTheLimit(int length, bool valid)
    {
        Assert.Equal(valid, Validate(Complete with { LastName = new string('a', length) }).IsValid);
    }
}
