using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Security;
using Mosaic.Infrastructure;

namespace Mosaic.Integration.Tests.Security;

public sealed class PasswordHasherRegistrationTests
{
    // a hasher per request would give each request its own gate, and hashes would run side by side
    [Fact]
    public async Task EveryRequestGetsTheSameHasher()
    {
        await using var provider = new ServiceCollection()
            .AddInfrastructure(ApiFactory.ConnectionString, ApiFactory.SetupTokenPath)
            .BuildServiceProvider();

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<PasswordHasher>(),
            second.ServiceProvider.GetRequiredService<PasswordHasher>());
    }
}
