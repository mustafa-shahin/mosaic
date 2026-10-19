using Mosaic.Contracts;
using Mosaic.Contracts.Health;

namespace Mosaic.Desktop.Services.Health;

public sealed class HealthApi(ApiClient api)
{
    public Task<ApiResponse<HealthResult>> GetAsync(CancellationToken cancellationToken) =>
        api.GetAsync<HealthResult>(HealthRoutes.Health, cancellationToken);
}
