using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Shell;

// For the previewer only.
internal static class ShellDesignData
{
    private static readonly SessionResult Person = new()
    {
        UserId = Guid.Empty,
        Email = "mara@example.com",
        FullName = "Mara Halbert"
    };

    public static ShellViewModel Default { get; } =
        new(DesignContext.Strings, DesignContext.Composition, NavBuilder.Keys(DesignContext.Composition), Person, DesignContext.SignOut);
}
