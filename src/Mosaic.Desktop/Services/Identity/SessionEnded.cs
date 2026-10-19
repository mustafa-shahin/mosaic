namespace Mosaic.Desktop.Services.Identity;

public enum SessionEndReason
{
    // the server answered 401 to a request that carried the token
    Refused,
    SignedOut,
    SignedOutEverywhere,
}

public sealed record SessionEnded(Session Session, SessionEndReason Reason);
