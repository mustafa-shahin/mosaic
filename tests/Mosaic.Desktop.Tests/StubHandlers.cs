using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Mosaic.Desktop.Tests;

// stand-ins for the network, so a test runs the real ApiClient and everything it decides about an answer

internal sealed class CannedResponseHandler(HttpStatusCode status, string body, string mediaType = "application/json")
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        });
}

internal sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw exception;
}

internal sealed class NeverRespondingHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

// one canned answer per path, for a screen that sends more than one request
internal sealed class RoutingHandler(IReadOnlyDictionary<string, (HttpStatusCode Status, string Body)> answers)
    : HttpMessageHandler
{
    public List<string> Paths { get; } = [];

    public List<string?> Bodies { get; } = [];

    public List<AuthenticationHeaderValue?> Authorizations { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        Paths.Add(path);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        Authorizations.Add(request.Headers.Authorization);

        // a path the test gave no answer for is a mistake in the test, so it must not read as a server refusal
        var (status, body) = answers.TryGetValue(path, out var answer)
            ? answer
            : throw new InvalidOperationException($"The test has no answer for {path}.");

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}

// answers like CannedResponseHandler and keeps what was sent, for the tests about the request itself
internal sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }

    public string? Path { get; private set; }

    public string? Body { get; private set; }

    public AuthenticationHeaderValue? Authorization { get; private set; }

    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Method = request.Method;
        Path = request.RequestUri?.AbsolutePath;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Authorization = request.Headers.Authorization;

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
