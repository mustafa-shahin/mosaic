namespace Mosaic.Contracts.Identity;

// what a person without an account can do here; a desktop cannot read a value it was not built with
public enum RegistrationState
{
    Unclaimed,
    Closed,
}
