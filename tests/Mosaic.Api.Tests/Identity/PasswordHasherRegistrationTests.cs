using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity;

namespace Mosaic.Api.Tests.Identity;

public sealed class PasswordHasherRegistrationTests
{
    // a hasher per request would give each request its own gate, and hashes would run side by side
    [Fact]
    public async Task EveryRequestGetsTheSameHasher()
    {
        await using var provider = new ServiceCollection()
            .AddAccounts(ApiFactory.SetupTokenPath)
            .BuildServiceProvider();

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<PasswordHasher>(),
            second.ServiceProvider.GetRequiredService<PasswordHasher>());
    }
}
