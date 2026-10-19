namespace Mosaic.Desktop.Features.Settings;

// For the previewer only.
internal static class SettingsDesignData
{
    public static SettingsViewModel Default { get; } = new(DesignContext.Strings, DesignContext.Store, DesignContext.SignOut);
}
