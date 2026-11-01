namespace Mosaic.Api.Validation;

// the names FluentValidation's LengthValidator gives the two bounds, CodePointLengthValidator uses the same names
internal static class LengthPlaceholders
{
    public const string Min = "MinLength";
    public const string Max = "MaxLength";
}
