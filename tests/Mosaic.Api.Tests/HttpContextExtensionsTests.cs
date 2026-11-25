using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Mosaic.Api.Tests;

public sealed class HttpContextExtensionsTests
{
    // the log prints the trace id on its own, so an id that also holds the span is not found there
    [Fact]
    public void CorrelationId_IsTheTraceIdOfTheRequest()
    {
        using var activity = new Activity("request").Start();

        Assert.Equal(activity.TraceId.ToHexString(), new DefaultHttpContext().CorrelationId());
    }

    // on a listener for both IPv4 and IPv6 an IPv4 client arrives as ::ffff:a.b.c.d, and the row holds the address a person knows
    [Fact]
    public void AuditOrigin_HoldsAnIPv4ClientInItsIPv4Form()
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.7");

        Assert.Equal(IPAddress.Parse("203.0.113.7"), context.AuditOrigin().IPAddress);
    }
}
