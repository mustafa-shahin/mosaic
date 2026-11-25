using FluentValidation;
using Mosaic.Contracts;

namespace Mosaic.Api.Validation;

// shared by every use case that answers with a page of a list
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        // one code per field, and every field is still checked
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(Paging.FirstPage).WithErrorCode(ErrorCodes.TooSmall)
            .LessThanOrEqualTo(Paging.MaxPage).WithErrorCode(ErrorCodes.TooLarge);

        RuleFor(request => request.PageSize)
            .GreaterThanOrEqualTo(Paging.MinPageSize).WithErrorCode(ErrorCodes.TooSmall)
            .LessThanOrEqualTo(Paging.MaxPageSize).WithErrorCode(ErrorCodes.TooLarge);
    }
}
