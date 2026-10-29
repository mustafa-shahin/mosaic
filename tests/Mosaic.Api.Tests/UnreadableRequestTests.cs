using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Mosaic.Contracts;

namespace Mosaic.Api.Tests;

// a body the API cannot read is the client's mistake: it keeps its 400 and carries the envelope in every environment
public sealed class UnreadableRequestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task BrokenJson_IsRefusedAsInvalid(string environment)
    {
        var (status, body) = await SignInAsync(environment, "{bad");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
        Assert.NotNull(body.TraceId);
    }

    // the desktop shows the code under the field the JSON reader could not read
    [Fact]
    public async Task AValueOfTheWrongType_NamesItsField()
    {
        var (status, body) = await SignInAsync("Production", """{"email":5,"password":"x"}""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(body.Error!.FieldErrors!["email"]).Code);
    }

    private async Task<(HttpStatusCode Status, ApiResponse Body)> SignInAsync(string environment, string json)
    {
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting(TrustedProxies.Key, "none"));

        var response = await host.CreateClient()
            .PostAsync("/v1/auth/sign-in", new StringContent(json, Encoding.UTF8, "application/json"), Token);

        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<ApiResponse>(MosaicJson.Options, Token))!);
    }
}
