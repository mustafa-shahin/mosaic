using Mosaic.Contracts.Identity;

namespace Mosaic.Desktop.Services.Identity;

// The session this process holds, in memory only, so closing the app forgets the token. Owned by App.
public sealed class CurrentSession
{
    public Session? Current { get; private set; }

    public event EventHandler<SessionEnded>? Ended;

    public Session Start(SignInResult signedIn)
    {
        Current = new Session(signedIn.Token, signedIn.Session);

        return Current;
    }

    // a refusal that arrives late for an older sign-in finds a different object here, so it ends nothing
    public void End(Session session, SessionEndReason reason)
    {
        if (!ReferenceEquals(Current, session))
        {
            return;
        }

        Current = null;
        Ended?.Invoke(this, new SessionEnded(session, reason));
    }
}
