using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Auditing;

namespace Mosaic.Desktop.Features.Auditing;

public sealed partial class AuditLogViewModel(AuditApi api, Localizer strings, TimeProvider clock) : ObservableObject, IDisposable
{
    // true while ClearFilters sets the filters back, so their change methods leave the loading to it
    private bool _clearing;

    // the page the last load asked for, which is not always the page on the screen
    private int _askedPage = Paging.FirstPage;

    private bool _choicesLoaded;

    public Localizer Strings { get; } = strings;

    [ObservableProperty]
    public partial IReadOnlyList<AuditRow> Entries { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsNoMatch))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(ShowsColumns))]
    public partial ListState State { get; set; } = ListState.Loading;

    public bool IsLoading => State == ListState.Loading;
    public bool IsReady => State == ListState.Ready;
    public bool IsEmpty => State == ListState.Empty;
    public bool IsNoMatch => State == ListState.NoMatch;
    public bool IsFailed => State == ListState.Failed;

    // the column names stand above real rows and above the grey rows that wait for them
    public bool ShowsColumns => State is ListState.Ready or ListState.Loading;

    // the grey rows that stand in for the table while a page loads with no rows on the screen
    public IReadOnlyList<int> PlaceholderRows { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    // why the last load did not work, in the app's language
    [ObservableProperty]
    public partial string? FailureText { get; set; }

    [ObservableProperty]
    public partial AuditRow? SelectedEntry { get; set; }

    // the row in the panel. It stays when the selection is cleared, so the panel does not leave empty
    [ObservableProperty]
    public partial AuditRow? ShownEntry { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PagePosition))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PagePosition))]
    public partial int TotalPages { get; set; } = 1;

    public string PagePosition => Strings.Translate("pager.position", new Dictionary<string, string>
    {
        ["page"] = Strings.Number(CurrentPage),
        ["count"] = Strings.Number(TotalPages),
    });

    [ObservableProperty]
    public partial string PageSummary { get; set; } = string.Empty;

    // the choices of the outcome switch, in the order it shows them
    public IReadOnlyList<OutcomeOption> Outcomes { get; } =
    [
        new(strings.Translate("filter.all"), null),
        new(strings.Translate("audit.succeeded"), true),
        new(strings.Translate("audit.failed"), false),
    ];

    // the place of the chosen outcome in Outcomes, so 0 is All
    [ObservableProperty]
    public partial int OutcomeIndex { get; set; }

    // the choices of the action filter: All, then the actions that audit rows hold, added once the server has sent them
    public ObservableCollection<ActionOption> Actions { get; } = [new(strings.Translate("filter.all"), null)];

    // the place of the chosen action in Actions, so 0 is All
    [ObservableProperty]
    public partial int ActionIndex { get; set; }

    // the person chosen in the person box; nobody chosen shows every person's rows
    [ObservableProperty]
    public partial PersonOption? SelectedPerson { get; set; }

    // the person box calls this a moment after the last key, and the server does the matching
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> SearchPeople => SearchPeopleAsync;

    // the choices of the date range, counted in calendar days with today
    public IReadOnlyList<DateRangeOption> DateRanges { get; } =
    [
        new(strings.Translate("filter.any_time"), null),
        new(strings.Translate("filter.today"), 1),
        new(LastDaysText(strings, 7), 7),
        new(LastDaysText(strings, 30), 30),
    ];

    // the place of the chosen date range in DateRanges, so 0 is any time
    [ObservableProperty]
    public partial int DateRangeIndex { get; set; }

    // the shell disposes the screen it leaves, so the loads still on their way stop instead of finishing for nobody
    public void Dispose()
    {
        LoadCommand.Cancel();
        LoadChoicesCommand.Cancel();
    }

    partial void OnSelectedEntryChanged(AuditRow? value)
    {
        if (value is not null)
        {
            ShownEntry = value;
        }
    }

    partial void OnOutcomeIndexChanged(int value) => LoadFirstPage();

    partial void OnActionIndexChanged(int value) => LoadFirstPage();

    partial void OnSelectedPersonChanged(PersonOption? value) => LoadFirstPage();

    partial void OnDateRangeIndexChanged(int value) => LoadFirstPage();

    // a filter changed, so the list starts again at page 1, unless the button is still setting the others back
    private void LoadFirstPage()
    {
        if (!_clearing)
        {
            LoadCommand.Execute(Paging.FirstPage);
        }
    }

    // a new request may start while one still loads: the command cancels the older one first, so the newest wins
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync(int pageNumber, CancellationToken cancellationToken)
    {
        _askedPage = pageNumber;

        // with no rows on the screen, grey rows show until the answer; rows that are on the screen stay and fade
        if (Entries.Count == 0)
        {
            State = ListState.Loading;
        }

        var filter = new AuditFilter
        {
            Succeeded = Outcomes[OutcomeIndex].Succeeded,
            Action = Actions[ActionIndex].Action,
            Person = SelectedPerson?.Id,
            From = DateRanges[DateRangeIndex].Days is { } days ? Strings.StartOfLastDays(clock.GetUtcNow(), days) : null,
        };

        try
        {
            var response = await api.GetEntriesAsync(pageNumber, filter, cancellationToken);

            // rows went away after the pager was drawn, so the asked page lies past the end: the last page comes instead
            if (response.Data is { } answer && answer.Items.Count == 0 && answer.TotalItems > 0)
            {
                response = await api.GetEntriesAsync(answer.TotalPages, filter, cancellationToken);
            }

            if (response.Success && response.Data is { } page)
            {
                Entries = AuditRow.ListFrom(page.Items, Strings);
                CurrentPage = page.CurrentPage;
                TotalPages = page.TotalPages;
                PageSummary = SummaryOf(page);
                State = StateOf(page);

                // choices that did not arrive earlier are asked for now that the server answers; Execute would cancel a load still on its way
                if (!_choicesLoaded && !LoadChoicesCommand.IsRunning)
                {
                    LoadChoicesCommand.Execute(null);
                }
            }
            else
            {
                // the server's code, or this app's own when the answer held neither data nor an error
                var code = response.Error?.Code ?? DesktopErrorCodes.UnexpectedResponse;

                // the old rows go, so none of them stays selected or open in the panel
                Entries = [];
                FailureText = Strings.TranslateCode(code, response.Error?.Params);
                State = ListState.Failed;
            }
        }
        // a newer load cancelled this one; if this escaped, the command would rethrow it on the UI thread
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    [RelayCommand]
    private async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await api.GetFilterChoicesAsync(cancellationToken);

            // a page that answers before the view asks starts a request of its own, so only the first answer fills the list
            if (response.Success && response.Data is { } choices && !_choicesLoaded)
            {
                foreach (var action in choices.Actions)
                {
                    Actions.Add(new ActionOption(Strings.Translate(action), action));
                }

                _choicesLoaded = true;
            }
        }
        // the screen was left while the choices loaded; if this escaped, the command would rethrow it on the UI thread
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<IEnumerable<object>> SearchPeopleAsync(string? text, CancellationToken cancellationToken)
    {
        var response = await api.SearchPeopleAsync(text, cancellationToken);

        if (!response.Success || response.Data is not { } people)
        {
            return [];
        }

        return people.Select(person => new PersonOption(person.Id, person.Name, person.Email, NameInitials.Of(person.Name)));
    }

    private static string LastDaysText(Localizer strings, int days) =>
        strings.Translate("filter.last_days", new Dictionary<string, string> { ["count"] = strings.Number(days) });

    // the places of the page's first and last row in the whole list, counted from 1
    private string SummaryOf(PageResult<AuditEntryResult> page)
    {
        var from = (page.CurrentPage - 1) * page.ItemsPerPage + 1;
        var to = Math.Min(page.CurrentPage * page.ItemsPerPage, page.TotalItems);

        return Strings.Translate("pager.summary", new Dictionary<string, string>
        {
            ["from"] = Strings.Number(from),
            ["to"] = Strings.Number(to),
            ["total"] = Strings.Number(page.TotalItems),
        });
    }

    // no rows mean an empty log or no match, and the server says which of the two it is
    private static ListState StateOf(PageResult<AuditEntryResult> page)
    {
        if (page.Items.Count > 0)
        {
            return ListState.Ready;
        }

        return page.IsEmptyWithoutFilters ? ListState.Empty : ListState.NoMatch;
    }

    // every filter back to its first choice, with one load for all of them instead of one load each
    [RelayCommand]
    private void ClearFilters()
    {
        _clearing = true;

        OutcomeIndex = 0;
        ActionIndex = 0;
        SelectedPerson = null;
        DateRangeIndex = 0;

        _clearing = false;

        LoadCommand.Execute(Paging.FirstPage);
    }

    // the button of the failure message: the same request again, with the filters as they are now
    [RelayCommand]
    private void TryAgain()
    {
        LoadCommand.Execute(_askedPage);
    }

    [RelayCommand]
    private void CloseDetails()
    {
        SelectedEntry = null;
    }
}
