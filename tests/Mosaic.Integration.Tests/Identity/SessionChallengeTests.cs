using System.Net;
using System.Net.Http.Headers;

namespace Mosaic.Integration.Tests.Identity;

// none of these reaches the database: no token, another scheme, or a value sign-in could not have issued
public sealed class SessionChallengeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null, "Bearer realm=\"mosaic\"")]
    [InlineData("Basic YWRhOmxvdmVsYWNl", "Bearer realm=\"mosaic\"")]
    [InlineData("Bearer not-a-token", "Bearer realm=\"mosaic\", error=\"invalid_token\"")]
    public async Task WithoutALiveSession_TheAnswerIs401WithAChallengeAndNoBody(string? authorization, string challenge)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/session");

        if (authorization is not null)
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        }

        using var response = await factory.CreateClient().SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(challenge, Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        Assert.Empty(await response.Content.ReadAsStringAsync(Token));
    }
}
