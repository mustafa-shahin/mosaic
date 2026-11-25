using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Desktop.Controls;
using Mosaic.Desktop.Features.Auditing;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Auditing;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Features.Auditing;

// AuditLogViewModelTests prove what the screen decides; these prove the window shows it and its controls reach it
public sealed class AuditLogViewTests : IDisposable
{
    private readonly AuditServer _server = new();

    [Fact]
    public async Task TheView_AsksForTheFirstPageAndTheChoices_WhenItGetsItsViewModel()
    {
        var (window, viewModel) = await Shown();

        Assert.Equal(["?page=1"], _server.EntryQueries);
        Assert.Equal(3, viewModel.Actions.Count);
        Assert.Equal(20, Table(window).ItemCount);
        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.True(window.GetVisualDescendants().OfType<Pager>().Single().IsEffectivelyVisible);
    }

    [Fact]
    public async Task AnEmptyLog_ShowsOnlyItsMessage()
    {
        _server.EntriesAnswer = page => (HttpStatusCode.OK, AuditServer.PageOf(page, 0, isEmptyWithoutFilters: true));

        var (window, viewModel) = await Shown();

        Assert.Equal(viewModel.Strings.Translate("audit.empty_title"), Assert.Single(VisibleMessages(window)).Title);
        Assert.False(Table(window).IsEffectivelyVisible);
        Assert.False(window.GetVisualDescendants().OfType<Pager>().Single().IsEffectivelyVisible);
    }

    [Fact]
    public async Task NoMatch_OffersClearFilters_WhichAsksOnce()
    {
        _server.EntriesAnswer = page => (HttpStatusCode.OK, AuditServer.PageOf(page, 0, isEmptyWithoutFilters: false));

        var (window, viewModel) = await Shown();

        viewModel.OutcomeIndex = 2;
        await Finished(viewModel.LoadCommand.ExecutionTask!);
        Dispatcher.UIThread.RunJobs();

        var message = Assert.Single(VisibleMessages(window));

        Assert.Equal(viewModel.Strings.Translate("audit.no_match_title"), message.Title);

        _server.EntryQueries.Clear();
        Click(window, ButtonIn(message, viewModel.Strings.Translate("filter.clear")));
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1"], _server.EntryQueries);
        Assert.Equal(0, viewModel.OutcomeIndex);
    }

    [Fact]
    public async Task AFailure_ShowsItsReason_AndTryAgainBringsTheRows()
    {
        _server.Down = true;

        var (window, viewModel) = await Shown();
        var message = Assert.Single(VisibleMessages(window));

        Assert.Equal(viewModel.Strings.Translate("audit.load_failed"), message.Title);
        Assert.Equal(viewModel.Strings.Translate(DesktopErrorCodes.ApiUnreachable), message.Text);

        _server.Down = false;
        Click(window, ButtonIn(message, viewModel.Strings.Translate("audit.try_again")));
        await Finished(viewModel.LoadCommand.ExecutionTask!);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(VisibleMessages(window));
        Assert.Equal(20, Table(window).ItemCount);
    }

    [Fact]
    public async Task ClickingAPageNumber_AsksForThatPage()
    {
        var (window, viewModel) = await Shown();

        Click(window, window.GetVisualDescendants().OfType<Button>().Single(button => button.DataContext is PageSlot { Number: 2 }));
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1", "?page=2"], _server.EntryQueries);
        Assert.Equal(2, viewModel.CurrentPage);
    }

    [Fact]
    public async Task TheOutcomeSwitchAndTheDropdowns_ReachTheFilters()
    {
        var (window, viewModel) = await Shown();
        var failed = window.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Single(item => item.DataContext is OutcomeOption { Succeeded: false });
        var boxes = window.GetVisualDescendants().OfType<FilterBox>().ToList();

        Click(window, failed);
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        boxes[0].SelectedIndex = 2;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        boxes[1].SelectedIndex = 1;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(
            ["?page=1", "?page=1&succeeded=false", "?page=1&succeeded=false&action=core.auth.sign_in",
                "?page=1&succeeded=false&action=core.auth.sign_in&from=2026-11-23T00%3A00%3A00.0000000%2B01%3A00"],
            _server.EntryQueries);
    }

    // the server has already matched, so the box must not filter the suggestions again by its own text
    [Fact]
    public async Task TypingAName_SuggestsWhomTheServerFound_AndAChoiceFilters()
    {
        _server.PeopleAnswer =
            """{"data":[{"id":"01a1dbd1-60a4-727b-bacd-c080468be164","name":"Clark Kent","email":"clark.kent@example.com"}],"success":true}""";

        var (window, viewModel) = await Shown();
        var box = window.GetVisualDescendants().OfType<AutoCompleteBox>().Single();

        box.Focus();
        window.KeyTextInput("kent");
        await Until(() => box.IsDropDownOpen);

        var suggestion = window.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Single(item => item.DataContext is PersonOption);

        Click(window, suggestion);
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?search=kent"], _server.PeopleQueries);
        Assert.Equal("?page=1&person=01a1dbd1-60a4-727b-bacd-c080468be164", _server.EntryQueries[^1]);
        Assert.Equal("Clark Kent", box.Text);
    }

    [Fact]
    public async Task ClickingARow_OpensItsDetailsWithTheAddress_AndEscapeClosesThem()
    {
        var (window, viewModel) = await Shown();
        var panel = window.GetVisualDescendants().OfType<Reveal>().Single();

        Click(window, Table(window).GetVisualDescendants().OfType<ListBoxItem>().First());

        var address = AddressField(panel, viewModel.Strings);

        Assert.True(panel.IsOpen);
        Assert.True(address.IsEffectivelyVisible);
        Assert.Contains(address.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "203.0.113.7");

        var copy = address.GetVisualDescendants().OfType<CopyButton>().Single();

        // the panel slides in before its buttons are where a click lands
        await Pump(TimeSpan.FromMilliseconds(500));
        await window.Clipboard!.SetTextAsync("before");
        Click(window, copy.GetVisualDescendants().OfType<Button>().Single());
        await Until(() => copy.Classes.Contains(":copied"));

        Assert.Equal("203.0.113.7", await window.Clipboard!.TryGetTextAsync());

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewModel.SelectedEntry);
        Assert.False(panel.IsOpen);
    }

    // the server sends no address to anyone but the owner
    [Fact]
    public async Task ARowWithoutAnAddress_ShowsNoAddressField()
    {
        _server.EntriesAnswer = _ => (HttpStatusCode.OK, """
            {"data":{"items":[
            {"id":"01a1dbd1-60a4-727b-bacd-c080468be164","occurredAt":"2026-11-23T08:00:00+00:00","action":"core.auth.sign_in","succeeded":true,"actorName":"Clark Kent","actorEmail":"clark.kent@example.com","targetType":"core.user","targetName":"Clark Kent","correlationId":"35959a94f3577b34da6a3ce929d0e0e4"}
            ],"totalItems":1,"itemsPerPage":20,"currentPage":1,"isEmptyWithoutFilters":false},"success":true}
            """);

        var (window, viewModel) = await Shown();
        var panel = window.GetVisualDescendants().OfType<Reveal>().Single();

        Click(window, Table(window).GetVisualDescendants().OfType<ListBoxItem>().Single());

        Assert.True(panel.IsOpen);
        Assert.False(AddressField(panel, viewModel.Strings).IsVisible);
    }

    public void Dispose() => _server.Dispose();

    private static Task Finished(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private async Task<(Window Window, AuditLogViewModel ViewModel)> Shown()
    {
        var viewModel = new AuditLogViewModel(
            new AuditApi(new ApiClient(new HttpClient(_server) { BaseAddress = new Uri("http://localhost") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve("en"), TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")),
            new Clock(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero)));
        var window = new Window { Width = 1360, Height = 848, Content = new AuditLogView { DataContext = viewModel } };

        window.Show();
        await Finished(viewModel.LoadCommand.ExecutionTask!);
        await Finished(viewModel.LoadChoicesCommand.ExecutionTask!);
        Dispatcher.UIThread.RunJobs();

        return (window, viewModel);
    }

    private static ListBox Table(Window window) =>
        window.GetVisualDescendants().OfType<ListBox>().Single(list => list.Classes.Contains("table"));

    private static List<StateMessage> VisibleMessages(Window window) =>
        window.GetVisualDescendants().OfType<StateMessage>().Where(message => message.IsVisible).ToList();

    private static Button ButtonIn(StateMessage message, string text) =>
        message.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, text));

    private static Border AddressField(Reveal panel, Localizer strings) =>
        panel.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Classes.Contains("detailsField"))
            .Single(field => field.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == strings.Translate("audit.ip_address")));

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < until)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static async Task Pump(TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < until)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
