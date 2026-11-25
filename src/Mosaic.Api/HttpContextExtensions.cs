using System.Diagnostics;
using System.Net;
using Mosaic.Domain.Auditing;

namespace Mosaic.Api;

internal static class HttpContextExtensions
{
    public static string CorrelationId(this HttpContext httpContext) =>
        Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;

    public static IPAddress? ClientAddress(this HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;

        // on a listener for both IPv4 and IPv6, Kestrel reports an IPv4 client as ::ffff:a.b.c.d
        if (address is not null && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address;
    }

    public static AuditOrigin AuditOrigin(this HttpContext httpContext) =>
        new(httpContext.ClientAddress(), httpContext.CorrelationId());
}
