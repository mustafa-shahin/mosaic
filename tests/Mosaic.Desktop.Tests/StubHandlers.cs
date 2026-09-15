using System.Net;
using System.Text;

namespace Mosaic.Desktop.Tests;

// Stand-ins for the network. Testing through the real ApiClient rather than around it means its
// exception handling is covered too, which is where its only real decisions live.

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
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
