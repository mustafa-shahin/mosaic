using System.Net;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Desktop.Features.Auditing;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Auditing;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Features.Auditing;

public sealed class AuditLogViewModelTests : IDisposable
{
    // Monday 23 November 2026, 9:00 in Berlin
    private static readonly DateTimeOffset MondayMorning = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);

    private static readonly PersonOption Clark =
        new(Guid.Parse("01a1dbd1-60a4-727b-bacd-c080468be164"), "Clark Kent", "clark.kent@example.com", "CK");

    private readonly AuditServer _server = new();

    [Fact]
    public async Task TheFirstPage_FillsTheRowsThePagerAndTheSummary()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        Assert.Equal(ListState.Ready, viewModel.State);
        Assert.Equal(20, viewModel.Entries.Count);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(3, viewModel.TotalPages);
        Assert.Equal("Showing 1 to 20 of 45", viewModel.PageSummary);
        Assert.Equal("Page 1 of 3", viewModel.PagePosition);
        Assert.Equal(["?page=1"], _server.EntryQueries);
    }

    [Fact]
    public async Task TheLastPage_EndsTheSummaryAtTheLastRow()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(3);

        Assert.Equal(5, viewModel.Entries.Count);
        Assert.Equal("Showing 41 to 45 of 45", viewModel.PageSummary);
        Assert.Equal("Page 3 of 3", viewModel.PagePosition);
    }

    [Fact]
    public async Task WhileAnotherPageLoads_TheRowsOnTheScreenStay()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        _server.Holding = true;

        var loading = viewModel.LoadCommand.ExecuteAsync(2);

        Assert.Equal(ListState.Ready, viewModel.State);
        Assert.Equal(20, viewModel.Entries.Count);

        _server.Release();
        await Finished(loading);

        Assert.Equal(2, viewModel.CurrentPage);
    }

    // the failure message would otherwise stay up until the answer arrives
    [Fact]
    public async Task TryingAgainAfterAFailure_ShowsTheGreyRowsWhileItLoads()
    {
        var viewModel = ViewModel();

        _server.Down = true;
        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        Assert.Equal(ListState.Failed, viewModel.State);

        _server.Down = false;
        _server.Holding = true;
        viewModel.TryAgainCommand.Execute(null);

        Assert.Equal(ListState.Loading, viewModel.State);
        Assert.True(viewModel.ShowsColumns);

        // the page that arrives also asks for the choices, which must not wait for a second release
        _server.Holding = false;
        _server.Release();
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(ListState.Ready, viewModel.State);
    }

    [Fact]
    public async Task AnEmptyLog_IsEmpty()
    {
        _server.EntriesAnswer = page => (HttpStatusCode.OK, AuditServer.PageOf(page, 0, isEmptyWithoutFilters: true));

        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        Assert.Equal(ListState.Empty, viewModel.State);
        Assert.False(viewModel.ShowsColumns);
    }

    [Fact]
    public async Task NoRowsForTheFilters_IsNoMatch()
    {
        _server.EntriesAnswer = page => (HttpStatusCode.OK, AuditServer.PageOf(page, 0, isEmptyWithoutFilters: false));

        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        Assert.Equal(ListState.NoMatch, viewModel.State);
        Assert.False(viewModel.ShowsColumns);
    }

    // no old row may stay selected, or open in the panel, under the failure message
    [Fact]
    public async Task AFailedLoad_GivesTheReasonInTheAppsLanguage_AndDropsTheOldRows()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        _server.Down = true;
        await viewModel.LoadCommand.ExecuteAsync(2);

        Assert.Equal(ListState.Failed, viewModel.State);
        Assert.Equal(viewModel.Strings.Translate(DesktopErrorCodes.ApiUnreachable), viewModel.FailureText);
        Assert.Empty(viewModel.Entries);
    }

    [Fact]
    public async Task ARefusal_IsTranslatedWithTheValuesThatCameWithIt()
    {
        _server.EntriesAnswer = _ => (HttpStatusCode.BadRequest, """{"error":{"code":"error.too_large","params":{"limit":"100"}}}""");

        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        Assert.Equal("Use 100 or less.", viewModel.FailureText);
    }

    // the page shown is 2, the page that failed is 3
    [Fact]
    public async Task TryAgain_AsksForThePageThatFailed()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(2);

        _server.Down = true;
        await viewModel.LoadCommand.ExecuteAsync(3);

        _server.Down = false;
        _server.EntryQueries.Clear();
        viewModel.TryAgainCommand.Execute(null);
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=3"], _server.EntryQueries);
        Assert.Equal(3, viewModel.CurrentPage);
    }

    // rows went away after the pager was drawn, so page 4 of 45 rows is empty
    [Fact]
    public async Task APagePastTheEnd_ShowsTheLastPageInstead()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(4);

        Assert.Equal(["?page=4", "?page=3"], _server.EntryQueries);
        Assert.Equal(3, viewModel.CurrentPage);
        Assert.Equal(5, viewModel.Entries.Count);
    }

    [Fact]
    public async Task ChoosingAnOutcome_AsksForPageOneOfIt()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(2);

        _server.EntryQueries.Clear();
        viewModel.OutcomeIndex = 2;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1&succeeded=false"], _server.EntryQueries);
    }

    [Fact]
    public async Task ChoosingAnAction_AsksForPageOneOfIt()
    {
        var viewModel = ViewModel();

        await viewModel.LoadChoicesCommand.ExecuteAsync(null);

        viewModel.ActionIndex = 2;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1&action=core.auth.sign_in"], _server.EntryQueries);
    }

    [Fact]
    public async Task ChoosingAPerson_AsksForPageOneOfTheirRows()
    {
        var viewModel = ViewModel();

        viewModel.SelectedPerson = Clark;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1&person=01a1dbd1-60a4-727b-bacd-c080468be164"], _server.EntryQueries);
    }

    // on a Monday the last seven days start on the Tuesday before, at midnight in Berlin
    [Fact]
    public async Task ChoosingTheLastSevenDays_AsksForTheRowsFromTheirFirstMidnight()
    {
        var viewModel = ViewModel();

        viewModel.DateRangeIndex = 2;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1&from=2026-11-17T00%3A00%3A00.0000000%2B01%3A00"], _server.EntryQueries);
    }

    // one reset after another would send one request each
    [Fact]
    public async Task ClearFilters_SetsEveryFilterBack_WithOneRequest()
    {
        var viewModel = ViewModel();

        await viewModel.LoadChoicesCommand.ExecuteAsync(null);

        viewModel.OutcomeIndex = 2;
        viewModel.ActionIndex = 1;
        viewModel.SelectedPerson = Clark;
        viewModel.DateRangeIndex = 1;
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        _server.EntryQueries.Clear();
        viewModel.ClearFiltersCommand.Execute(null);
        await Finished(viewModel.LoadCommand.ExecutionTask!);

        Assert.Equal(["?page=1"], _server.EntryQueries);
        Assert.Equal(0, viewModel.OutcomeIndex);
        Assert.Equal(0, viewModel.ActionIndex);
        Assert.Null(viewModel.SelectedPerson);
        Assert.Equal(0, viewModel.DateRangeIndex);
    }

    [Fact]
    public async Task TheActionChoices_AreAllThenTheActionsTheServerSent_InWords()
    {
        var viewModel = ViewModel();

        await viewModel.LoadChoicesCommand.ExecuteAsync(null);

        Assert.Equal(["All", "Registration", "Sign-in"], viewModel.Actions.Select(option => option.Text));
        Assert.Equal([null, AuditActions.Register, AuditActions.SignIn], viewModel.Actions.Select(option => option.Action));
    }

    [Fact]
    public async Task ChoicesThatCannotLoad_LeaveOnlyAll()
    {
        _server.Down = true;

        var viewModel = ViewModel();

        await viewModel.LoadChoicesCommand.ExecuteAsync(null);

        Assert.Equal([null], viewModel.Actions.Select(option => option.Action));
    }

    // the screen opened while the server was away; the first page that loads afterwards brings the choices too
    [Fact]
    public async Task ChoicesThatFailed_AreAskedForAgain_WhenAPageLoads()
    {
        var viewModel = ViewModel();

        _server.Down = true;
        await viewModel.LoadChoicesCommand.ExecuteAsync(null);

        _server.Down = false;
        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);
        await Finished(viewModel.LoadChoicesCommand.ExecutionTask!);

        Assert.Equal([null, AuditActions.Register, AuditActions.SignIn], viewModel.Actions.Select(option => option.Action));
    }

    [Fact]
    public async Task ChoicesThatArrived_AreNotAskedForAgain()
    {
        var viewModel = ViewModel();

        await viewModel.LoadChoicesCommand.ExecuteAsync(null);
        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);
        await viewModel.LoadCommand.ExecuteAsync(2);
        await Finished(viewModel.LoadChoicesCommand.ExecutionTask!);

        Assert.Equal(3, viewModel.Actions.Count);
    }

    // a second Execute would cancel the choices on their way and send them again
    [Fact]
    public async Task ChoicesOnTheirWay_AreLeftAlone_WhenThePageArrivesFirst()
    {
        var viewModel = ViewModel();

        _server.Holding = true;

        var choices = viewModel.LoadChoicesCommand.ExecuteAsync(null);

        _server.Holding = false;
        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        _server.Release();
        await Finished(choices);

        Assert.Equal(0, _server.Cancelled);
        Assert.Equal(3, viewModel.Actions.Count);
    }

    [Fact]
    public async Task ThePersonBox_SuggestsThePeopleTheServerFound()
    {
        _server.PeopleAnswer =
            """{"data":[{"id":"01a1dbd1-60a4-727b-bacd-c080468be164","name":"Clark Kent","email":"clark.kent@example.com"}],"success":true}""";

        var viewModel = ViewModel();

        var suggestions = await viewModel.SearchPeople("kent", TestContext.Current.CancellationToken);

        Assert.Equal(["?search=kent"], _server.PeopleQueries);
        Assert.Equal(Clark, Assert.Single(suggestions));
    }

    [Fact]
    public async Task ASearchThatFails_SuggestsNobody()
    {
        _server.Down = true;

        var viewModel = ViewModel();

        Assert.Empty(await viewModel.SearchPeople("kent", TestContext.Current.CancellationToken));
    }

    // the shell disposes the screen it leaves
    [Fact]
    public async Task Dispose_StopsTheLoadsOnTheirWay()
    {
        _server.Holding = true;

        var viewModel = ViewModel();
        var page = viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);
        var choices = viewModel.LoadChoicesCommand.ExecuteAsync(null);

        viewModel.Dispose();
        await Finished(page);
        await Finished(choices);

        Assert.Equal(2, _server.Cancelled);
        Assert.Empty(viewModel.Entries);
        Assert.Single(viewModel.Actions);
    }

    // a later click cancels the earlier load, so a late answer cannot replace the newer page
    [Fact]
    public async Task TheNewestPageAsked_IsThePageShown()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        _server.Holding = true;
        viewModel.LoadCommand.Execute(2);

        var second = viewModel.LoadCommand.ExecutionTask!;

        Assert.True(viewModel.LoadCommand.CanExecute(3));

        viewModel.LoadCommand.Execute(3);

        var third = viewModel.LoadCommand.ExecutionTask!;

        await Finished(second);
        _server.Release();
        await Finished(third);

        Assert.Equal(1, _server.Cancelled);
        Assert.Equal(3, viewModel.CurrentPage);
    }

    [Fact]
    public async Task ASelectedRow_IsShownInThePanel()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        var row = viewModel.Entries[3];

        viewModel.SelectedEntry = row;

        Assert.Same(row, viewModel.ShownEntry);
    }

    // the panel keeps its row while it moves out
    [Fact]
    public async Task ClosingTheDetails_ClearsTheSelection_AndKeepsTheRowInThePanel()
    {
        var viewModel = ViewModel();

        await viewModel.LoadCommand.ExecuteAsync(Paging.FirstPage);

        var row = viewModel.Entries[3];

        viewModel.SelectedEntry = row;
        viewModel.CloseDetailsCommand.Execute(null);

        Assert.Null(viewModel.SelectedEntry);
        Assert.Same(row, viewModel.ShownEntry);
    }

    public void Dispose() => _server.Dispose();

    // a load that the code under test should have cancelled would otherwise wait for ever
    private static Task Finished(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private AuditLogViewModel ViewModel() =>
        new(new AuditApi(new ApiClient(new HttpClient(_server) { BaseAddress = new Uri("http://localhost") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve("en"), TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")),
            new Clock(MondayMorning));
}
