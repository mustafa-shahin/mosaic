using Mosaic.Contracts.Identity;

namespace Mosaic.Desktop.Services.Identity;

// one sign-in: the token and the person from the sign-in answer, never changed afterwards
public sealed class Session(string token, SessionResult person)
{
    public string Token { get; } = token;

    public SessionResult Person { get; } = person;
}
