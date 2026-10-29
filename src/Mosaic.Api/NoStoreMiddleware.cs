using Microsoft.Net.Http.Headers;

namespace Mosaic.Api;

// every answer is about this instance or the person asking, so no cache may keep one, refusals included
internal sealed class NoStoreMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // before the endpoint runs, so an endpoint whose answer may be kept can replace it with its own
        context.Response.Headers.CacheControl = CacheControlHeaderValue.NoStoreString;

        return next(context);
    }
}
