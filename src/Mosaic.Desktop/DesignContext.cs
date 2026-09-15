using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// What every design class needs and none of them should build for itself. Names no feature, so adding
// a screen never edits this file.
internal static class DesignContext
{
    public static Localizer Strings { get; } = Localizer.Load(LanguageCatalog.Resolve(LanguageCatalog.DefaultCode));

    // its own folder, so previewing can never read or overwrite the real settings.json
    public static SettingsStore Store { get; } = new(Path.Combine(Path.GetTempPath(), "MosaicDesign"));

    // nothing in a preview calls the API; the address exists so a stray call fails instead of hanging
    public static ApiClient Api { get; } = new(new HttpClient { BaseAddress = new Uri("http://localhost") });

    // the same composition the running app builds, so a previewed sidebar is the real sidebar
    public static IDesktopComposition Composition { get; } =
        new CoreComposition(new DesktopServices(Api, Store, (_, _) => { }));
}
