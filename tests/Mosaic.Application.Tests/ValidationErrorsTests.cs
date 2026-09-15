using Mosaic.Application.Validation;
using Mosaic.Contracts;

namespace Mosaic.Application.Tests.Validation;

public sealed class ValidationErrorsTests
{
    [Fact]
    public void ThrowIfAny_WithNothingAdded_DoesNotThrow()
    {
        var errors = new ValidationErrors();

        errors.ThrowIfAny();

        Assert.False(errors.Any);
    }

    [Fact]
    public void Add_TwoRulesOnOneField_KeepsBothInOrder()
    {
        var errors = new ValidationErrors();

        errors.Add("password", "error.too_short", new Dictionary<string, string> { ["min"] = "12" });
        errors.Add("password", "error.no_digit");

        var thrown = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        var forPassword = thrown.FieldErrors["password"];

        Assert.Equal(2, forPassword.Count);
        Assert.Equal("12", forPassword[0].Params?["min"]);
        Assert.Null(forPassword[1].Params);
    }

    [Fact]
    public void ThrowIfAny_WhenItThrows_ReportsValidationFailed()
    {
        var errors = new ValidationErrors();

        errors.Add("email", "error.required");

        var thrown = Assert.Throws<ValidationException>(errors.ThrowIfAny);

        Assert.Equal(ErrorCodes.ValidationFailed, thrown.Code);
    }
}