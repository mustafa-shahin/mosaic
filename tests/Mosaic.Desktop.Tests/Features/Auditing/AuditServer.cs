using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;

namespace Mosaic.Desktop.Tests.Features.Auditing;

// the audit addresses of a server the test controls: it keeps every request, and while Holding is on an answer waits for Release
internal sealed class AuditServer : HttpMessageHandler
{
    private const string Entries = ApiPaths.Audit + AuditRoutes.Entries;
    private const string Filters = ApiPaths.Audit + AuditRoutes.Filters;
    private const string People = ApiPaths.Audit + AuditRoutes.People;

    private TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // the text after the path of each request for entries, such as "?page=2&succeeded=false"
    public List<string> EntryQueries { get; } = [];

    public List<string> PeopleQueries { get; } = [];

    // the requests that were cancelled while they waited
    public int Cancelled { get; private set; }

    public bool Holding { get; set; }

    // every request fails the way it does when nothing answers at the address
    public bool Down { get; set; }

    // what the server answers for each page number; a log of 45 rows unless a test says otherwise
    public Func<int, (HttpStatusCode Status, string Body)> EntriesAnswer { get; set; } = page => (HttpStatusCode.OK, PageOf(page, 45));

    public string ChoicesAnswer { get; set; } = """{"data":{"actions":["core.auth.register","core.auth.sign_in"]},"success":true}""";

    public string PeopleAnswer { get; set; } = """{"data":[],"success":true}""";

    // one page of a log of sign-ins one minute apart, newest first, 20 rows to a page
    public static string PageOf(int page, int totalItems, bool isEmptyWithoutFilters = false)
    {
        var first = (page - 1) * Paging.DefaultPageSize;
        var count = Math.Clamp(totalItems - first, 0, Paging.DefaultPageSize);
        var items = new List<string>();

        for (var index = first; index < first + count; index++)
        {
            var id = new Guid(index, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            var moment = new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero).AddMinutes(-index);

            items.Add(string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"id":"{{id}}","occurredAt":"{{moment:O}}","action":"core.auth.sign_in","succeeded":true,"actorName":"Bruce Wayne","actorEmail":"bruce.wayne@example.com","targetType":"core.user","targetName":"Bruce Wayne","correlationId":"{{id:N}}","ipAddress":"203.0.113.7"}"""));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"data":{"items":[{{string.Join(',', items)}}],"totalItems":{{totalItems}},"itemsPerPage":{{Paging.DefaultPageSize}},"currentPage":{{page}},"isEmptyWithoutFilters":{{(isEmptyWithoutFilters ? "true" : "false")}}},"success":true}""");
    }

    public void Release()
    {
        var gate = _gate;

        _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.RequestUri.Query;

        if (path == Entries)
        {
            EntryQueries.Add(query);
        }
        else if (path == People)
        {
            PeopleQueries.Add(query);
        }

        if (Holding)
        {
            try
            {
                await _gate.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled++;
                throw;
            }
        }

        if (Down)
        {
            throw new HttpRequestException("no route to host");
        }

        var (status, body) = path switch
        {
            Entries => EntriesAnswer(PageNumberOf(query)),
            Filters => (HttpStatusCode.OK, ChoicesAnswer),
            People => (HttpStatusCode.OK, PeopleAnswer),
            _ => throw new InvalidOperationException($"The test has no answer for {path}."),
        };

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static int PageNumberOf(string query) =>
        int.Parse(HttpUtility.ParseQueryString(query)[Paging.PageParameter]!, CultureInfo.InvariantCulture);
}
