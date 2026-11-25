using FluentIcons.Common;
using Mosaic.Contracts.Navigation;
using Mosaic.Desktop.Features.Auditing;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Auditing;
using Mosaic.Desktop.Services.Health;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// What a composition needs in order to build screens. Owned by App and alive for the whole process.
public sealed record DesktopServices(ApiClient Api, SettingsStore Settings, SignOut SignOut, LanguageChoice Language);

// Besides App.axaml, the only file outside Features/ that names a feature.
public static class CoreComposition
{
    public static DesktopComposition Build(DesktopServices services) => new(
        [
            new(NavSections.Overview, "nav.overview"),
            new(NavSections.Administration, "nav.administration"),
        ],
        [
            new(NavSections.Overview, NavEntries.Dashboard, Symbol.Board,
                strings => new HealthViewModel(new HealthApi(services.Api), strings)),

            new(NavSections.Administration, NavEntries.Audit, Symbol.History,
                strings => new AuditLogViewModel(new AuditApi(services.Api), strings, TimeProvider.System)),

            new(NavSections.Administration, NavEntries.Settings, Symbol.Settings,
                strings => new SettingsViewModel(strings, services.Settings, services.SignOut, services.Language)),
        ]);
}
