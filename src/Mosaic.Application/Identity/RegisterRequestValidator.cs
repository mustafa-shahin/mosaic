using System.Net.Mail;
using FluentValidation;
using Mosaic.Application.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Application.Identity;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        // one code per field, and every field is still checked
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(UserLimits.EmailLength).WithErrorCode(ErrorCodes.TooLong)
            .Must(BeASingleAddress).WithErrorCode(ErrorCodes.InvalidEmail);

        RuleFor(request => request.FirstName)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(UserLimits.NameLength).WithErrorCode(ErrorCodes.TooLong);

        RuleFor(request => request.LastName)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(UserLimits.NameLength).WithErrorCode(ErrorCodes.TooLong);

        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .SetValidator(new CodePointLengthValidator<RegisterRequest>(UserLimits.PasswordMinLength, int.MaxValue))
            .WithErrorCode(ErrorCodes.TooShort)
            .SetValidator(new CodePointLengthValidator<RegisterRequest>(0, UserLimits.PasswordMaxLength))
            .WithErrorCode(ErrorCodes.TooLong)
            .Must(NotBeCommon).WithErrorCode(ErrorCodes.CommonPassword);
    }

    // the built-in EmailAddress() only looks for a single '@', so "Ada <ada@example.com>" passes it
    private static bool BeASingleAddress(string? value) =>
        MailAddress.TryCreate(value, out var parsed) && parsed.Address == value;

    // this Must overload also passes the request, the check needs its email and names
    private static bool NotBeCommon(RegisterRequest request, string? password) =>
        password is null || !CommonPasswords.IsCommon(password, request.Email, request.FirstName, request.LastName);
}