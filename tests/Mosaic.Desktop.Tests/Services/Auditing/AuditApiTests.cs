using System.Net;
using Mosaic.Contracts.Auditing;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Auditing;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Tests.Services.Auditing;

// the addresses as literals on purpose: an installed desktop sends exactly these, so renaming a constant must fail here
public sealed class AuditApiTests
{
    private const string EmptyPage =
        """{"data":{"items":[],"totalItems":0,"itemsPerPage":20,"currentPage":1,"isEmptyWithoutFilters":true},"success":true}""";

    private static AuditApi ApiWith(HttpMessageHandler handler) =>
        new(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession()));

    [Fact]
    public async Task Entries_GetsTheV1AddressWithThePageAndEveryFilter()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, EmptyPage);
        var filter = new AuditFilter
        {
            Succeeded = false,
            Action = "core.auth.sign_in",
            Person = Guid.Parse("01a1dbd1-60a4-727b-bacd-c080468be164"),
            From = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(1)),
        };

        await ApiWith(recorder).GetEntriesAsync(2, filter, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, recorder.Method);
        Assert.Equal("/v1/audit/entries", recorder.Path);
        Assert.Equal(
            "?page=2&succeeded=false&action=core.auth.sign_in&person=01a1dbd1-60a4-727b-bacd-c080468be164&from=2026-11-17T00%3A00%3A00.0000000%2B01%3A00",
            recorder.Query);
    }

    [Fact]
    public async Task Entries_WithNoFilter_SendsOnlyThePage()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, EmptyPage);

        await ApiWith(recorder).GetEntriesAsync(1, new AuditFilter(), TestContext.Current.CancellationToken);

        Assert.Equal("?page=1", recorder.Query);
    }

    [Fact]
    public async Task People_GetsTheV1AddressWithTheText()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"data":[],"success":true}""");

        await ApiWith(recorder).SearchPeopleAsync("bruce w", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/audit/people", recorder.Path);
        Assert.Equal("?search=bruce%20w", recorder.Query);
    }

    [Fact]
    public async Task Filters_GetsTheV1Address()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, """{"data":{"actions":[]},"success":true}""");

        await ApiWith(recorder).GetFilterChoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/audit/filters", recorder.Path);
        Assert.Equal(string.Empty, recorder.Query);
    }

    // a row for someone who is not the owner comes without ipAddress at all
    [Fact]
    public async Task Entries_ReadsTheAnswerTheApiWrites()
    {
        const string answer = """
            {"data":{"items":[
            {"id":"01a1dbd1-60a4-727b-bacd-c080468be164","occurredAt":"2026-11-25T09:41:12.489544+00:00","action":"core.auth.sign_in","succeeded":false,"errorCode":"error.invalid_credentials","targetType":"core.user","targetName":"Bruce Wayne","correlationId":"35959a94f3577b34da6a3ce929d0e0e4"}
            ],"totalItems":41,"itemsPerPage":20,"currentPage":3,"isEmptyWithoutFilters":false},"success":true}
            """;

        var response = await ApiWith(new CannedResponseHandler(HttpStatusCode.OK, answer))
            .GetEntriesAsync(3, new AuditFilter(), TestContext.Current.CancellationToken);

        var page = response.Data!;
        var entry = Assert.Single(page.Items);

        Assert.Equal(41, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.False(page.IsEmptyWithoutFilters);
        Assert.False(entry.Succeeded);
        Assert.Equal("error.invalid_credentials", entry.ErrorCode);
        Assert.Null(entry.ActorName);
        Assert.Null(entry.IPAddress);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 41, 12, 489, 544, TimeSpan.Zero), entry.OccurredAt);
    }
}
