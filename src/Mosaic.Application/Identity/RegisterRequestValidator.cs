using System.Net.Mail;
using FluentValidation;
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
            .NotEmpty().WithErrorCode(ErrorCodes.Required);
    }

    // the built-in EmailAddress() only looks for a single '@', so "Ada <ada@example.com>" passes it
    private static bool BeASingleAddress(string? value) =>
        MailAddress.TryCreate(value, out var parsed) && parsed.Address == value;
}