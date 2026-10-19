using Mosaic.Contracts;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Shell.SignIn;

// For the previewer only. A failed sign-in just after first run, because it is the state with the most on screen.
internal static class SignInDesignData
{
    public static SignInViewModel Refused { get; } =
        new(new AuthApi(DesignContext.Api), DesignContext.Strings)
        {
            Email = "user@example.com",
            Password = "test password for view data",
            Note = SignInNote.AccountReady,
            ErrorCode = ErrorCodes.ServerBusy
        };
}
