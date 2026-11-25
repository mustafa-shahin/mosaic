using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// A heading in the sidebar. The composition declares them in the order they appear.
public sealed record NavSectionDefinition(string Key, string CaptionKey);

// One screen a feature offers the sidebar.
public sealed record NavContribution(
    string SectionKey, string Key, Symbol Icon, Func<Localizer, ObservableObject> Open);
