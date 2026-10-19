using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// What every design class shares. Names no feature, so adding a screen never edits this file.
internal static class DesignContext
{
    public static Localizer Strings { get; } = Localizer.Load(LanguageCatalog.Resolve(LanguageCatalog.DefaultCode));

    // its own folder, so previewing can never read or overwrite the real settings.json
    public static SettingsStore Store { get; } = new(Path.Combine(Path.GetTempPath(), "MosaicDesign"));

    public static CurrentSession Session { get; } = new();

    // nothing in a preview calls the API; the address gives the previews a server name to show
    public static ApiClient Api { get; } = new(new HttpClient { BaseAddress = new Uri("http://localhost") }, Session);

    public static SignOut SignOut { get; } = new(new AuthApi(Api), Session);

    // the same composition the running app builds, so a previewed sidebar is the real sidebar
    public static IDesktopComposition Composition { get; } =
        new CoreComposition(new DesktopServices(Api, Store, SignOut, (_, _) => { }));
}
