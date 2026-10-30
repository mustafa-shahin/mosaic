using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Features.Settings;

public sealed class SettingsViewTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mosaic-tests", Guid.CreateVersion7().ToString());

    private readonly Localizer _strings = Localizer.Load(LanguageCatalog.Resolve("en"));

    private readonly CurrentSession _session = new();

    // SettingsViewModelTests prove what the command does. This proves the card explains it and its button reaches it.
    [Fact]
    public async Task TheSessionsCard_SaysWhatItDoesAndItsButtonSignsOutEverywhere()
    {
        var server = new RecordingHandler(HttpStatusCode.OK, """{"success":true}""");
        var signOut = new SignOut(
            new AuthApi(new ApiClient(new HttpClient(server) { BaseAddress = new Uri("http://localhost:5175/") }, _session)), _session);
        var settings = new SettingsViewModel(_strings, new SettingsStore(_folder), signOut);

        _session.Start(SignInResults.Ada());

        var window = new Window { Width = 1360, Height = 848, Content = new SettingsView { DataContext = settings } };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == _strings.Translate("settings.sessions_text"));

        var button = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => Equals(candidate.Content, _strings.Translate("settings.sign_out_everywhere")));
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);

        await settings.SignOutEverywhereCommand.ExecutionTask!;

        Assert.Equal(ApiPaths.Auth + AuthRoutes.SignOutEverywhere, server.Path);
        Assert.Null(_session.Current);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
