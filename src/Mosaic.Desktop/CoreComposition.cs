using Avalonia.Controls.Templates;
using FluentIcons.Common;
using Mosaic.Contracts.Navigation;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Health;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// What a composition needs in order to build screens. Owned by App and alive for the whole process.
public sealed record DesktopServices(
    ApiClient Api, SettingsStore Settings, SignOut SignOut, EventHandler<LanguageOption> LanguageChosen);

// Besides App.axaml, the only file outside Features/ that names a feature.
public sealed class CoreComposition(DesktopServices services) : IDesktopComposition
{
    public IReadOnlyList<NavSectionDefinition> Sections { get; } =
    [
        new(NavSections.Overview, "nav.overview"),
        new(NavSections.Administration, "nav.administration"),
    ];

    public IReadOnlyList<NavContribution> Contributions { get; } =
    [
        // local, because the screen that reports an unreachable server cannot wait for that server to list it
        new(NavSections.Overview, NavEntries.Dashboard, Symbol.Board, local: true,
            strings => new HealthViewModel(new HealthApi(services.Api), strings)),

        new(NavSections.Administration, NavEntries.Settings, Symbol.Settings, local: true,
            strings =>
            {
                var settings = new SettingsViewModel(strings, services.Settings, services.SignOut);

                // App listens for this, and the view model that raises it is created here
                settings.LanguageChosen += services.LanguageChosen;

                return settings;
            }),
    ];

    // core's templates are declared in App.axaml
    public IReadOnlyList<IDataTemplate> Templates { get; } = [];
}
