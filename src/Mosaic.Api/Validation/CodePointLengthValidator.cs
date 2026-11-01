using System.Text;
using FluentValidation;
using FluentValidation.Validators;

namespace Mosaic.Api.Validation;

// code points after NFC, the text the hasher hashes; MinimumLength counts UTF-16 units of the raw text
internal sealed class CodePointLengthValidator<T>(int min, int max) : PropertyValidator<T, string?>
{
    public override string Name => nameof(CodePointLengthValidator<T>);

    public override bool IsValid(ValidationContext<T> context, string? value)
    {
        // a missing value is NotEmpty's to report
        if (value is null)
        {
            return true;
        }

        var length = 0;

        foreach (var _ in value.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            length++;
        }

        if (length >= min && length <= max)
        {
            return true;
        }

        context.MessageFormatter
            .AppendArgument(LengthPlaceholders.Min, min)
            .AppendArgument(LengthPlaceholders.Max, max);

        return false;
    }
}
