using CommunityToolkit.Mvvm.ComponentModel;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Settings;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop.Tests;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));

    private ShellViewModel Shell(string? startAt = null) => Shell(_strings, startAt);

    private ShellViewModel Shell(Localizer strings, string? startAt = null)
    {
        var api = new ApiClient(new HttpClient(new ThrowingHandler(new InvalidOperationException("must not be called"))));

        return new ShellViewModel(
            strings,
            new HealthViewModel(api, strings),
            new SettingsViewModel(strings, new SettingsStore(_folder)),
            startAt);
    }

    // by target type, not by label
    private static NavEntry EntryLeadingTo<T>(ShellViewModel shell) where T : ObservableObject =>
        shell.Sections.SelectMany(section => section.Entries).Single(entry => entry.Target is T);

    [Fact]
    public void Current_OnStart_IsTheDashboard()
    {
        var shell = Shell();

        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.Equal(_strings.Translate("nav.dashboard"), shell.Title);
        Assert.Equal("nav.dashboard", shell.ActiveKey);
    }

    [Fact]
    public void Go_ToAnotherEntry_ShowsItMovesTheMarkerAndRenamesTheHeader()
    {
        var shell = Shell();
        var settings = EntryLeadingTo<SettingsViewModel>(shell);

        shell.GoCommand.Execute(settings);

        Assert.IsType<SettingsViewModel>(shell.Current);
        Assert.True(settings.IsActive);
        Assert.False(EntryLeadingTo<HealthViewModel>(shell).IsActive);
        Assert.Equal(_strings.Translate("nav.settings"), shell.Title);
        Assert.Equal("nav.settings", shell.ActiveKey);
    }

    [Fact]
    public void StartAt_APreviousScreen_OpensThereInsteadOfTheDashboard()
    {
        var rebuilt = Shell(startAt: "nav.settings");

        Assert.IsType<SettingsViewModel>(rebuilt.Current);
        Assert.Equal("nav.settings", rebuilt.ActiveKey);
        Assert.True(EntryLeadingTo<SettingsViewModel>(rebuilt).IsActive);
    }

    [Fact]
    public void StartAt_AScreenThatNoLongerExists_FallsBackToTheDashboard()
    {
        var rebuilt = Shell(startAt: "nav.gone");

        Assert.IsType<HealthViewModel>(rebuilt.Current);
        Assert.Equal("nav.dashboard", rebuilt.ActiveKey);
    }

    [Fact]
    public void Labels_ComeFromTheLanguageTheShellWasBuiltWith()
    {
        var german = Localizer.Load(LanguageCatalog.Resolve("de"));

        var shell = Shell(german);

        Assert.Equal(german.Translate("nav.overview"), shell.Sections[0].Caption);
        Assert.Equal(german.Translate("nav.dashboard"), shell.Title);
        Assert.NotEqual(_strings.Translate("nav.overview"), shell.Sections[0].Caption);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
