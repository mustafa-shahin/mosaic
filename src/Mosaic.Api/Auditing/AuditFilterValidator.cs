using FluentValidation;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;

namespace Mosaic.Api.Auditing;

public sealed class AuditFilterValidator : AbstractValidator<AuditFilter>
{
    public AuditFilterValidator()
    {
        // ASP.NET refuses an empty succeeded, person or from itself; an empty action is a valid string and needs this rule
        RuleFor(filter => filter.Action)
            .NotEmpty().WithErrorCode(ErrorCodes.InvalidValue)
            .When(filter => filter.Action is not null);
    }
}
