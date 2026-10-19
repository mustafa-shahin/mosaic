using Mosaic.Contracts;
using Mosaic.Contracts.Identity;

namespace Mosaic.Desktop.Services.Identity;

// The desktop's side of AuthController, so no screen writes a path or chooses a verb.
public sealed class AuthApi(ApiClient api)
{
    private const string Prefix = ApiVersions.V1Path + "/" + AuthRoutes.Prefix + "/";

    public Uri? BaseAddress => api.BaseAddress;

    public Task<ApiResponse<RulesResult>> GetRulesAsync(CancellationToken cancellationToken) =>
        api.GetAsync<RulesResult>(Prefix + AuthRoutes.Rules, cancellationToken);

    public Task<ApiResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        api.PostAsync<RegisterRequest>(Prefix + AuthRoutes.Register, request, cancellationToken);

    public Task<ApiResponse<SignInResult>> SignInAsync(SignInRequest request, CancellationToken cancellationToken) =>
        api.PostAsync<SignInRequest, SignInResult>(Prefix + AuthRoutes.SignIn, request, cancellationToken);

    public Task<ApiResponse> SignOutAsync(Session session, CancellationToken cancellationToken) =>
        api.PostWithoutBodyAsync(Prefix + AuthRoutes.SignOut, session, cancellationToken);

    public Task<ApiResponse> SignOutEverywhereAsync(Session session, CancellationToken cancellationToken) =>
        api.PostWithoutBodyAsync(Prefix + AuthRoutes.SignOutEverywhere, session, cancellationToken);
}
