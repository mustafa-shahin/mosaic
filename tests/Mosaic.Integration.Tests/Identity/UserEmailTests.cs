using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Persistence;
using Npgsql;

namespace Mosaic.Integration.Tests.Identity;

[Collection<PostgresCollectionDefinition>]
[Trait("Requires", "Docker")]
public sealed class UserEmailTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASecondAccountWithTheSameAddress_IsRefusedByTheDatabase()
    {
        var email = UniqueEmail();

        await Save(User.Register(email, "Ada", "Lovelace", "hash", null, Now));

        var refused = await Assert.ThrowsAsync<DbUpdateException>(
            () => Save(User.Register(email, "Grace", "Hopper", "hash", null, Now)));

        AssertViolates(refused, "ix_users_email");
    }

    [Fact]
    public async Task ADeletedAccount_StillHoldsItsAddress()
    {
        var email = UniqueEmail();
        var deleted = User.Register(email, "Ada", "Lovelace", "hash", null, Now);

        deleted.SoftDelete(Now, null);

        await Save(deleted);

        var refused = await Assert.ThrowsAsync<DbUpdateException>(
            () => Save(User.Register(email, "Grace", "Hopper", "hash", null, Now)));

        AssertViolates(refused, "ix_users_email");
    }

    private async Task Save(User user)
    {
        await using var scope = postgres.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        context.Users.Add(user);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertViolates(DbUpdateException refused, string constraint)
    {
        var error = Assert.IsType<PostgresException>(refused.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    // the database lives for the whole run, so every test needs an address no other test used
    private static string UniqueEmail() => $"{Guid.CreateVersion7():N}@mosaic.test";
}
