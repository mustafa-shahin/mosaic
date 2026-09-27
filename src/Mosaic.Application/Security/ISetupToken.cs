namespace Mosaic.Application.Security;

public enum SetupTokenResult
{
    Missing,
    Invalid,
    Accepted,
}

// an unclaimed instance goes to whoever registers first, so that person proves they can read the server's disk
public interface ISetupToken
{
    // writes the token first when there is none, so a refusal always points at a file that exists
    SetupTokenResult Verify(string? supplied);

    void Discard();
}
