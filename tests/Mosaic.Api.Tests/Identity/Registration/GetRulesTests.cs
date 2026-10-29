using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Api.Identity;
using Mosaic.Api.Identity.Registration;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Settings;
using Mosaic.Persistence;

namespace Mosaic.Api.Tests.Identity.Registration;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class GetRulesTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnInstanceWithNoClaim_IsUnclaimed()
    {
        await postgres.ResetAsync(Token);

        var rules = await Rules();

        Assert.Equal(RegistrationState.Unclaimed, rules.Registration);
    }

    [Fact]
    public async Task AnUnclaimedInstance_WritesTheTokenFileWhenThereIsNone()
    {
        await postgres.ResetAsync(Token);
        File.Delete(postgres.SetupTokenPath);

        await Rules();

        Assert.True(File.Exists(postgres.SetupTokenPath));
    }

    // whoever reads the token can claim the instance, so no other user of the server's system may read it
    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task AnUnclaimedInstance_WritesATokenFileOnlyItsOwnerCanRead()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no unix file mode.");

        await postgres.ResetAsync(Token);
        File.Delete(postgres.SetupTokenPath);

        await Rules();

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(postgres.SetupTokenPath));
    }

    [Fact]
    public async Task AnUnclaimedInstance_KeepsTheTokenThatIsAlreadyThere()
    {
        await postgres.ResetAsync(Token);

        await Rules();

        Assert.Equal(PostgresFixture.SetupToken, await File.ReadAllTextAsync(postgres.SetupTokenPath, Token));
    }

    [Fact]
    public async Task AClaimedInstance_IsClosedAndWritesNoTokenFile()
    {
        await Claimed();
        File.Delete(postgres.SetupTokenPath);

        var rules = await Rules();

        Assert.Equal(RegistrationState.Closed, rules.Registration);
        Assert.False(File.Exists(postgres.SetupTokenPath));
    }

    // the limits the register validator applies, so the guidance and a refusal name the same numbers
    [Fact]
    public async Task ThePasswordRule_IsTheValidatorsLimits()
    {
        await postgres.ResetAsync(Token);

        var rules = await Rules();

        Assert.Equal(PasswordPolicy.MinLength, rules.Password.MinLength);
        Assert.Equal(PasswordPolicy.MaxLength, rules.Password.MaxLength);
    }

    private async Task<RulesResult> Rules()
    {
        await using var scope = postgres.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<GetRules>().HandleAsync(Token);
    }

    // the claim alone, without a user, which is all the rules read
    private async Task Claimed()
    {
        await postgres.ResetAsync(Token);

        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.InstanceSettings.Add(InstanceSettings.Claim(Now));

        await context.SaveChangesAsync(Token);
    }
}
