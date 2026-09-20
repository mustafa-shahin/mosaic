using Mosaic.Application.Identity;
using Mosaic.Application.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Application.Tests.Validation;

public sealed class ValidationResultExtensionsTests
{
    private static readonly RegisterRequest Complete = new()
    {
        Email = "ada@example.com",
        FirstName = "Ada",
        LastName = "Lovelace",
        Password = "correct horse battery staple",
    };

    private static ValidationException Thrown(RegisterRequest request) =>
        Assert.Throws<ValidationException>(() => new RegisterRequestValidator().Validate(request).ThrowIfInvalid());

    [Fact]
    public void AValidResult_DoesNotThrow()
    {
        Assert.Null(Record.Exception(() => new RegisterRequestValidator().Validate(Complete).ThrowIfInvalid()));
    }

    [Fact]
    public void AFailure_IsReportedAsValidationFailed()
    {
        Assert.Equal(ErrorCodes.ValidationFailed, Thrown(new RegisterRequest()).Code);
    }

    [Fact]
    public void EveryFailingField_IsKeyedByItsWireName()
    {
        var fields = Thrown(new RegisterRequest()).FieldErrors;

        Assert.Equal("email firstName lastName password", string.Join(' ', fields.Keys.Order(StringComparer.Ordinal)));
        Assert.All(fields.Values, errors => Assert.Single(errors));
    }

    // MaximumLength also reports a MinLength of 0, which must not become the limit
    [Fact]
    public void TooLong_CarriesTheMaximumAsItsOnlyParameter()
    {
        var thrown = Thrown(Complete with { FirstName = new string('a', UserLimits.NameLength + 1) });

        var error = Assert.Single(thrown.FieldErrors["firstName"]);
        var parameter = Assert.Single(error.Params!);

        Assert.Equal(ErrorCodes.TooLong, error.Code);
        Assert.Equal(ErrorParams.Limit, parameter.Key);
        Assert.Equal("100", parameter.Value);
    }

    [Fact]
    public void ACodeWithoutALimit_CarriesNoParameters()
    {
        var error = Assert.Single(Thrown(Complete with { Email = null }).FieldErrors["email"]);

        Assert.Equal(ErrorCodes.Required, error.Code);
        Assert.Null(error.Params);
    }
}
