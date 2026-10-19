using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services.Identity;

public sealed class CurrentSessionTests
{
    [Fact]
    public void Start_HoldsTheTokenAndThePersonFromTheSignInAnswer()
    {
        var session = new CurrentSession();
        var signedIn = SignInResults.Ada("mosaic_the-token");

        var started = session.Start(signedIn);

        Assert.Same(started, session.Current);
        Assert.Equal("mosaic_the-token", started.Token);
        Assert.Same(signedIn.Session, started.Person);
    }

    [Fact]
    public void End_ForgetsTheSessionAndSaysWhyOnce()
    {
        var session = new CurrentSession();
        var started = session.Start(SignInResults.Ada());
        var ends = new List<SessionEnded>();

        session.Ended += (_, ended) => ends.Add(ended);

        session.End(started, SessionEndReason.SignedOutEverywhere);
        session.End(started, SessionEndReason.Refused);

        Assert.Null(session.Current);
        Assert.Equal([new SessionEnded(started, SessionEndReason.SignedOutEverywhere)], ends);
    }

    [Fact]
    public void End_OfASessionThatIsNoLongerCurrent_ChangesNothing()
    {
        var session = new CurrentSession();
        var older = session.Start(SignInResults.Ada("mosaic_older"));
        var newer = session.Start(SignInResults.Ada("mosaic_newer"));
        var ends = new List<SessionEnded>();

        session.Ended += (_, ended) => ends.Add(ended);

        session.End(older, SessionEndReason.Refused);

        Assert.Same(newer, session.Current);
        Assert.Empty(ends);
    }
}
