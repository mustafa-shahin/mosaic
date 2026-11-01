using FluentValidation.Results;
using Mosaic.Api.Identity;
using Mosaic.Api.Identity.Registration;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Identity;

namespace Mosaic.Api.Tests.Identity.Registration;

public sealed class RegisterRequestValidatorTests
{
    private const string Passphrase = "correct horse battery staple";

    private static readonly RegisterRequest Complete = new()
    {
        Email = "ada@example.com",
        FirstName = "Ada",
        LastName = "Lovelace",
        Password = Passphrase,
    };

    private static readonly RegisterRequest Tom = Complete with
    {
        Email = "tom.smith@example.com",
        FirstName = "Tom",
        LastName = "Smith",
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
        var email = "Ada <" + new string('a', User.EmailMaxLength) + "@example.com>";

        var failure = Assert.Single(Validate(Complete with { Email = email }).Errors);

        Assert.Equal(ErrorCodes.TooLong, failure.ErrorCode);
    }

    [Theory]
    [InlineData(User.NameMaxLength, true)]
    [InlineData(User.NameMaxLength + 1, false)]
    public void AFirstName_MayBeAsLongAsTheLimit(int length, bool valid)
    {
        Assert.Equal(valid, Validate(Complete with { FirstName = new string('a', length) }).IsValid);
    }

    [Theory]
    [InlineData(User.NameMaxLength, true)]
    [InlineData(User.NameMaxLength + 1, false)]
    public void ALastName_MayBeAsLongAsTheLimit(int length, bool valid)
    {
        Assert.Equal(valid, Validate(Complete with { LastName = new string('a', length) }).IsValid);
    }

    [Theory]
    [InlineData(PasswordPolicy.MinLength - 1, ErrorCodes.TooShort)]
    [InlineData(PasswordPolicy.MinLength, null)]
    public void APassword_MustReachTheMinimum(int length, string? code)
    {
        var errors = Validate(Complete with { Password = Passphrase[..length] }).Errors;

        Assert.Equal(code, errors.SingleOrDefault()?.ErrorCode);
    }

    // an emoji is two UTF-16 units and one code point; u followed by U+0308, the two dots as their own character, is one letter after NFC
    [Theory]
    [InlineData("\U0001F600", 14, ErrorCodes.TooShort)]
    [InlineData("\U0001F600", 15, null)]
    [InlineData("u\u0308", 14, ErrorCodes.TooShort)]
    [InlineData("u\u0308", 15, null)]
    [InlineData("\U0001F600", 128, null)]
    [InlineData("\U0001F600", 129, ErrorCodes.TooLong)]
    public void APassword_IsMeasuredInCodePointsAfterNfc(string unit, int count, string? code)
    {
        var errors = Validate(Complete with { Password = string.Concat(Enumerable.Repeat(unit, count)) }).Errors;

        Assert.Equal(code, errors.SingleOrDefault()?.ErrorCode);
    }

    [Theory]
    [InlineData("passwordpassword")]
    [InlineData("PasswordPassword")]
    public void AListedPassword_IsCommonInAnyCase(string password)
    {
        var failure = Assert.Single(Validate(Complete with { Password = password }).Errors);

        Assert.Equal(ErrorCodes.CommonPassword, failure.ErrorCode);
    }

    // alone, or with digits, symbols or spaces before or after it
    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("ada@example.com1")]
    [InlineData("AdaLovelace1815!")]
    [InlineData("LovelaceAda1815!")]
    [InlineData("Ada Lovelace 1815")]
    [InlineData("Lovelace Ada 1815")]
    [InlineData("mosaic123456789")]
    [InlineData("!!Mosaic2026!!!!!")]
    public void TheNamesTheAddressOrMosaic_AreCommon(string password)
    {
        var failure = Assert.Single(Validate(Complete with { Password = password }).Errors);

        Assert.Equal(ErrorCodes.CommonPassword, failure.ErrorCode);
    }

    [Fact]
    public void ThePartBeforeTheAt_IsCommon()
    {
        var failure = Assert.Single(Validate(Tom with { Password = "tom.smith2026!!!" }).Errors);

        Assert.Equal(ErrorCodes.CommonPassword, failure.ErrorCode);
    }

    // the whole password is compared, so a name inside a word or a sentence does not count
    [Theory]
    [InlineData("tomorrow tomatoes")]
    [InlineData("Tom loves his dog Max forever")]
    public void ANameInsideTheText_IsNotCommon(string password)
    {
        Assert.True(Validate(Tom with { Password = password }).IsValid);
    }

    [Fact]
    public void ADigitsOnlyPassword_IsNotTakenForAMissingName()
    {
        var failure = Assert.Single(Validate(Complete with { FirstName = null, Password = "0193847562039485" }).Errors);

        Assert.Equal(nameof(RegisterRequest.FirstName), failure.PropertyName);
    }
}
