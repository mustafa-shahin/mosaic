using Mosaic.Domain.Authorization;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Tests.Authorization;

namespace Mosaic.Domain.Tests.Identity;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    // not an owner, because an owner passes every permission check whatever the roles grant
    private static User NewUser()
    {
        var user = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);

        user.SetOwner(false);

        return user;
    }

    private static Role RoleWith(string name, params Permission[] permissions)
        => Role.Create(name, null, permissions, Now);

    [Theory]
    [InlineData("  Ada@Example.COM  ", "ada@example.com")]
    [InlineData("ADA@EXAMPLE.COM", "ada@example.com")]
    public void CreateOwner_NormalisesTheEmail(string entered, string stored)
    {
        Assert.Equal(stored, User.CreateOwner(entered, " Ada ", " Lovelace ", "hash", Now).Email);
    }

    [Fact]
    public void CreateOwner_IsActiveImmediately()
    {
        var owner = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);

        Assert.True(owner.IsOwner);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public void CreateOwner_GivesEachUserTheirOwnStamp()
    {
        var first = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);
        var second = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);

        Assert.NotEqual(Guid.Empty, first.SecurityStamp);
        Assert.NotEqual(first.SecurityStamp, second.SecurityStamp);
    }

    [Fact]
    public void ChangePassword_RotatesTheStamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.ChangePassword("new-hash");

        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void UpgradePasswordHash_KeepsTheStamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.UpgradePasswordHash("stronger-hash");

        Assert.Equal("stronger-hash", user.PasswordHash);
        Assert.Equal(before, user.SecurityStamp);
    }

    [Fact]
    public void SetTotpSecret_RotatesTheStamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.SetTotpSecret("secret");

        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void ConfirmTotp_RotatesTheStamp()
    {
        var user = NewUser();
        user.SetTotpSecret("secret");
        var before = user.SecurityStamp;

        user.ConfirmTotp();

        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void DisableTotp_RotatesTheStamp()
    {
        var user = NewUser();
        user.SetTotpSecret("secret");
        user.ConfirmTotp();
        var before = user.SecurityStamp;

        user.DisableTotp();

        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void EndAllSessions_RotatesTheStamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.EndAllSessions();

        Assert.NotEqual(before, user.SecurityStamp);
    }

    // none of these should sign anyone out: every signed-in request reads IsActive, and a session holds no roles
    [Fact]
    public void RoutineChanges_KeepTheStamp()
    {
        var user = NewUser();
        var dev = RoleWith("Dev", TestPermissions.Deploy);
        user.SetTotpSecret("secret");
        var before = user.SecurityStamp;

        user.RecordLogin(Now);
        user.RegisterFailedLogin(Now);
        user.ResetLoginFailures();
        user.Activate();
        user.Deactivate();
        user.AddRole(dev);
        user.RemoveRole(dev);
        user.Reencrypt(secret => secret + "-rewrapped");

        Assert.Equal(before, user.SecurityStamp);
    }

    [Fact]
    public void ResolvePermissions_WithTwoRoles_IsTheUnionOfBoth()
    {
        var user = NewUser();

        user.AddRole(RoleWith("Dev", TestPermissions.Deploy));
        user.AddRole(RoleWith("Finance", TestPermissions.ViewProjects));

        var resolved = user.ResolvePermissions();

        Assert.True(resolved.Has(TestPermissions.Deploy));
        Assert.True(resolved.Has(TestPermissions.ViewProjects));
    }

    [Fact]
    public void ResolvePermissions_AfterRemovingOneRole_KeepsWhatTheOtherGranted()
    {
        var user = NewUser();
        var dev = RoleWith("Dev", TestPermissions.Deploy);

        user.AddRole(dev);
        user.AddRole(RoleWith("Finance", TestPermissions.ViewProjects));
        user.RemoveRole(dev);

        var resolved = user.ResolvePermissions();

        Assert.False(resolved.Has(TestPermissions.Deploy));
        Assert.True(resolved.Has(TestPermissions.ViewProjects));
    }

    [Fact]
    public void ResolvePermissions_AfterEveryRoleIsRemoved_IsEmpty()
    {
        var user = NewUser();
        var dev = RoleWith("Dev", TestPermissions.Deploy);

        user.AddRole(dev);
        user.RemoveRole(dev);

        Assert.Empty(user.ResolvePermissions().Granted);
    }

    [Fact]
    public void ResolvePermissions_WithNoRoles_IsEmpty()
    {
        Assert.Empty(NewUser().ResolvePermissions().Granted);
    }

    // IsOwner answers before the granted set is looked at, so even a key nobody declared passes
    [Fact]
    public void ResolvePermissions_ForAnOwner_AnswersTrueWithNothingGranted()
    {
        var resolved = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now).ResolvePermissions();

        Assert.True(resolved.Has(TestPermissions.ManageSystem));
        Assert.True(resolved.Has(new Permission("pro.tenants.manage")));
        Assert.Empty(resolved.Granted);
    }

    [Fact]
    public void AddRole_WithARoleAlreadyHeld_DoesNotHoldItTwice()
    {
        var user = NewUser();
        var dev = RoleWith("Dev", TestPermissions.Deploy);

        user.AddRole(dev);
        user.AddRole(dev);

        Assert.Single(user.Roles);
    }

    [Fact]
    public void RegisterFailedLogin_BelowTheThreshold_LeavesTheAccountOpen()
    {
        var user = NewUser();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.False(user.IsLockedOut(Now));
    }

    [Theory]
    [InlineData(5, 30)]
    [InlineData(6, 60)]
    [InlineData(7, 120)]
    [InlineData(10, 960)]
    [InlineData(11, 1800)]
    [InlineData(20, 1800)]
    public void RegisterFailedLogin_DoublesTheLockoutUpToHalfAnHour(int attempts, int seconds)
    {
        var user = NewUser();

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.Equal(Now.AddSeconds(seconds), user.LockoutUntil);
        Assert.True(user.IsLockedOut(Now));
    }

    [Fact]
    public void IsLockedOut_OnceTheLockoutHasPassed_IsFalse()
    {
        var user = NewUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.False(user.IsLockedOut(Now.AddSeconds(31)));
    }

    [Fact]
    public void ResetLoginFailures_UnlocksTheAccount()
    {
        var user = NewUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(Now);
        }

        user.ResetLoginFailures();

        Assert.Null(user.LockoutUntil);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void Reencrypt_WithNoSecretStored_RewrapsNothing()
    {
        Assert.Equal(0, NewUser().Reencrypt(secret => secret + "!"));
    }

    [Fact]
    public void Reencrypt_LeavesTheEnabledStateAlone()
    {
        var user = NewUser();

        user.SetTotpSecret("old");
        user.ConfirmTotp();

        Assert.Equal(1, user.Reencrypt(secret => secret + "-rewrapped"));
        Assert.Equal("old-rewrapped", user.EncryptedTotpSecret);
        Assert.True(user.TotpEnabled);
    }

    [Fact]
    public void SoftDelete_RecordsWhenAndWho_AndRestoreClearsIt()
    {
        var user = NewUser();
        var owner = Guid.CreateVersion7();

        user.SoftDelete(Now, owner);

        Assert.True(user.IsDeleted);
        Assert.Equal(Now, user.DeletedAt);
        Assert.Equal(owner, user.DeletedByUserId);

        user.Restore();

        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.DeletedByUserId);
    }

    [Fact]
    public void SoftDelete_DoesNotTouchIsActive()
    {
        var owner = User.CreateOwner("ada@example.com", "Ada", "Lovelace", "hash", Now);

        owner.SoftDelete(Now, null);

        Assert.True(owner.IsActive);
    }
}
