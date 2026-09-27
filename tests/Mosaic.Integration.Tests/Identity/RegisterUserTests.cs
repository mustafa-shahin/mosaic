using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Application.Identity;
using Mosaic.Application.Security;
using Mosaic.Application.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class RegisterUserTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheFirstRegistration_BecomesTheActiveOwnerAndClaimsTheInstance()
    {
        await Reset();

        var result = await Register(UniqueEmail());

        await using var scope = postgres.CreateScope();
        var user = await Context(scope).Users.SingleAsync(Token);

        Assert.Equal(RegistrationOutcome.Owner, result.Outcome);
        Assert.True(user.IsOwner);
        Assert.True(user.IsActive);
        Assert.Single(await Context(scope).InstanceSettings.ToListAsync(Token));
    }

    [Fact]
    public async Task WithoutAToken_AnUnclaimedInstanceRefusesAndWritesTheFile()
    {
        await Reset();
        File.Delete(postgres.SetupTokenPath);

        var refused = await Assert.ThrowsAsync<NotPermittedException>(() => Register(UniqueEmail(), setupToken: null));

        Assert.Equal(ErrorCodes.SetupTokenRequired, refused.Code);
        Assert.True(File.Exists(postgres.SetupTokenPath));
        Assert.Equal(0, await CountUsers());
    }

    [Fact]
    public async Task AWrongToken_IsRefusedAndSavesNothing()
    {
        await Reset();

        var refused = await Assert.ThrowsAsync<NotPermittedException>(() => Register(UniqueEmail(), setupToken: "guessed"));

        Assert.Equal(ErrorCodes.SetupTokenInvalid, refused.Code);
        Assert.Equal(0, await CountUsers());
    }

    // a stranger without the token learns nothing about the rules and never costs a hash
    [Fact]
    public async Task TheTokenIsCheckedBeforeTheRequestIsValidated()
    {
        await Reset();

        var refused = await Assert.ThrowsAsync<NotPermittedException>(
            () => Register("not an address", password: "short", setupToken: null));

        Assert.Equal(ErrorCodes.SetupTokenRequired, refused.Code);
    }

    [Fact]
    public async Task ATokenPastedWithALineBreak_IsAccepted()
    {
        await Reset();

        var result = await Register(UniqueEmail(), setupToken: $" {PostgresFixture.SetupToken}\n");

        Assert.Equal(RegistrationOutcome.Owner, result.Outcome);
    }

    [Fact]
    public async Task TheClaim_DeletesTheFile()
    {
        await Reset();

        await Register(UniqueEmail());

        Assert.False(File.Exists(postgres.SetupTokenPath));
    }

    // the file is spent before the save, so a save that fails leaves no token that works again later
    [Fact]
    public async Task AClaimWhoseSaveFails_StillLeavesNoFile()
    {
        await Reset();
        await Execute(
            "CREATE FUNCTION refuse_insert() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''refused''; END';"
            + "CREATE TRIGGER refuse_users BEFORE INSERT ON users FOR EACH ROW EXECUTE FUNCTION refuse_insert()");

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Register(UniqueEmail()));
        }
        finally
        {
            await Execute("DROP TRIGGER refuse_users ON users; DROP FUNCTION refuse_insert()");
        }

        Assert.False(File.Exists(postgres.SetupTokenPath));
        Assert.Equal(0, await CountUsers());
    }

    [Fact]
    public async Task AClaimedInstance_IgnoresTheToken()
    {
        await Reset();
        await Register(UniqueEmail());
        await Open();

        var result = await Register(UniqueEmail(), setupToken: null);

        Assert.Equal(RegistrationOutcome.Pending, result.Outcome);
    }

    [Fact]
    public async Task UnderClosed_ALaterRegistrationIsRefusedAndSavesNothing()
    {
        await Reset();
        await Register(UniqueEmail());

        var refused = await Assert.ThrowsAsync<NotPermittedException>(() => Register(UniqueEmail()));

        Assert.Equal(ErrorCodes.RegistrationClosed, refused.Code);
        Assert.Equal(1, await CountUsers());
    }

    // nothing in the request is checked first, so even the owner's own address gets this answer
    [Fact]
    public async Task UnderClosed_TheRefusalComesBeforeTheAddressCheck()
    {
        await Reset();

        var owner = UniqueEmail();

        await Register(owner);

        var refused = await Assert.ThrowsAsync<NotPermittedException>(() => Register(owner));

        Assert.Equal(ErrorCodes.RegistrationClosed, refused.Code);
    }

    [Fact]
    public async Task UnderOpen_ALaterRegistrationLandsPending()
    {
        await Reset();
        await Register(UniqueEmail());
        await Open();

        var email = UniqueEmail();
        var result = await Register(email);
        var user = await Find(email);

        Assert.Equal(RegistrationOutcome.Pending, result.Outcome);
        Assert.False(user.IsOwner);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task AnAddressAlreadyRegistered_IsAFieldError()
    {
        await Reset();

        var email = UniqueEmail();

        await Register(email);
        await Open();

        var refused = await Assert.ThrowsAsync<ValidationException>(() => Register(email.ToUpperInvariant()));

        Assert.Equal(ErrorCodes.AlreadyTaken, Assert.Single(refused.FieldErrors["email"]).Code);
    }

    [Fact]
    public async Task AnAddressHeldByADeletedAccount_IsStillTaken()
    {
        await Reset();
        await Register(UniqueEmail());
        await Open();

        var email = UniqueEmail();

        await Register(email);

        await using (var scope = postgres.CreateScope())
        {
            var context = Context(scope);
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email, Token);

            user.SoftDelete(Now, null);

            await context.SaveChangesAsync(Token);
        }

        var refused = await Assert.ThrowsAsync<ValidationException>(() => Register(email));

        Assert.Equal(ErrorCodes.AlreadyTaken, Assert.Single(refused.FieldErrors["email"]).Code);
    }

    [Fact]
    public async Task ATakenAddressAndAShortPassword_AreReportedTogether()
    {
        await Reset();

        var email = UniqueEmail();

        await Register(email);
        await Open();

        var refused = await Assert.ThrowsAsync<ValidationException>(() => Register(email, password: "short"));

        Assert.Equal(ErrorCodes.AlreadyTaken, Assert.Single(refused.FieldErrors["email"]).Code);
        Assert.Equal(ErrorCodes.TooShort, Assert.Single(refused.FieldErrors["password"]).Code);
    }

    [Fact]
    public async Task ThePartsOfTheRequest_AreTrimmedBeforeTheyAreStored()
    {
        await Reset();

        var email = UniqueEmail();

        await Register($"  {email}  ", firstName: "  Ada  ", lastName: "  Lovelace  ");

        var user = await Find(email);

        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
    }

    [Fact]
    public async Task ARequestThatFailsTheRules_SavesNothing()
    {
        await Reset();

        await Assert.ThrowsAsync<ValidationException>(() => Register("not an address", password: "short"));

        await using var scope = postgres.CreateScope();

        Assert.Equal(0, await CountUsers());
        Assert.Empty(await Context(scope).InstanceSettings.ToListAsync(Token));
    }

    [Fact]
    public async Task ThePasswordIsStoredAsAHashThatVerifies()
    {
        await Reset();

        var email = UniqueEmail();

        await Register(email);

        var user = await Find(email);

        await using var scope = postgres.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        Assert.DoesNotContain(Password, user.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, await hasher.VerifyAsync(Password, user.PasswordHash, Token));
    }

    // both requests find no owner before either one saves, and the key lets only one claim through
    [Fact]
    public async Task TwoFirstRegistrationsAtOnce_LeaveExactlyOneOwner()
    {
        await Reset();

        var first = Register(UniqueEmail());
        var second = Register(UniqueEmail());
        var refusedByTheKey = 0;

        foreach (var attempt in new[] { first, second })
        {
            try
            {
                await attempt;
            }
            catch (DbUpdateException)
            {
                refusedByTheKey++;
            }
        }

        await using var scope = postgres.CreateScope();

        Assert.Equal(1, refusedByTheKey);
        Assert.Equal(1, await CountUsers());
        Assert.Single(await Context(scope).InstanceSettings.ToListAsync(Token));
    }

    private async Task<RegisterResult> Register(
        string email,
        string firstName = "Ada",
        string lastName = "Lovelace",
        string password = Password,
        string? setupToken = PostgresFixture.SetupToken)
    {
        await using var scope = postgres.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<RegisterUser>().HandleAsync(
            new RegisterRequest
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Password = password,
                SetupToken = setupToken,
            },
            Token);
    }

    private async Task<User> Find(string email)
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).Users.IgnoreQueryFilters().SingleAsync(user => user.Email == email, Token);
    }

    private async Task<int> CountUsers()
    {
        await using var scope = postgres.CreateScope();

        return await Context(scope).Users.IgnoreQueryFilters().CountAsync(Token);
    }

    // no endpoint changes the policy yet, so the test writes it directly
    private Task Open() => Execute("UPDATE instance_settings SET registration_policy = 'Open'");

    private Task Reset() => postgres.ResetAsync(Token);

    private async Task Execute(string sql)
    {
        await using var scope = postgres.CreateScope();

        await Context(scope).Database.ExecuteSqlRawAsync(sql, Token);
    }

    private static ManagementDbContext Context(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";
}
