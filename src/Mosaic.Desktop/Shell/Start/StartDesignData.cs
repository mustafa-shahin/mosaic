using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Shell.Start;

// For the previewer only. A failed check, because it is the state with the most on screen.
internal static class StartDesignData
{
    public static StartViewModel Unreachable { get; } =
        new(new AuthApi(DesignContext.Api), DesignContext.Strings)
        {
            ErrorCode = DesktopErrorCodes.ApiUnreachable
        };
}
