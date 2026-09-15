using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Shell;

// Names no feature any more. The previewer is handed the composition the running app is handed.
internal static class ShellDesignData
{
    public static ShellViewModel Default { get; } =
        new(DesignContext.Strings, DesignContext.Composition, NavBuilder.Keys(DesignContext.Composition));
}
