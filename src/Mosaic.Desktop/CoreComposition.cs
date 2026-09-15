using Avalonia.Controls.Templates;
using FluentIcons.Common;
using Mosaic.Contracts.Navigation;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// What a composition needs in order to build screens. Owned by App and alive for the whole process.
public sealed record DesktopServices(ApiClient Api, SettingsStore Settings, EventHandler<LanguageOption> LanguageChosen);

// The only place in core that names a feature, and the whole answer to what the sidebar holds.
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
            strings => new HealthViewModel(services.Api, strings)),

        new(NavSections.Administration, NavEntries.Settings, Symbol.Settings, local: true,
            strings =>
            {
                var settings = new SettingsViewModel(strings, services.Settings);

                // App listens for this, and the object that raises it is born here
                settings.LanguageChosen += services.LanguageChosen;

                return settings;
            }),
    ];

    // core's templates are declared in App.axaml, where a missing view is a compile error
    public IReadOnlyList<IDataTemplate> Templates { get; } = [];
}
