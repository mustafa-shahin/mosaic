using Mosaic.Domain.Authorization;
using Mosaic.Domain.Common;

namespace Mosaic.Domain.Identity;

public sealed class User : SoftDeletableEntity
{
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 100;
    public const int EncryptedTotpSecretMaxLength = 500;

    private const int LockoutThreshold = 5;
    private const int BaseLockoutSeconds = 30;
    private const int MaxLockoutSeconds = 1800;

    private readonly List<Role> _roles = [];

    private User()
    {
    }

    public string Email { get; private set; } = null!;

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string FullName => $"{FirstName} {LastName}";

    public string PasswordHash { get; private set; } = null!;

    // Copied onto every session when it starts. A new value ends them all on their next request.
    public Guid SecurityStamp { get; private set; }

    public bool IsOwner { get; private set; }

    // Whether the person may sign in and keep using a session. Unrelated to IsDeleted.
    public bool IsActive { get; private set; }

    public IReadOnlyList<Role> Roles => _roles;

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public string? EncryptedTotpSecret { get; private set; }

    public bool TotpEnabled { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockoutUntil { get; private set; }

    public static User CreateOwner(
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Email = Normalize(email),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            SecurityStamp = Guid.NewGuid(),
            IsOwner = true,
            IsActive = true,
            CreatedAt = now,
        };

    // The union of the permissions of every role held.
    public PrincipalPermissions ResolvePermissions()
        => PrincipalPermissions.ForUser(IsOwner, _roles.SelectMany(role => role.Permissions));

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    public bool IsLockedOut(DateTimeOffset now) => LockoutUntil is { } until && until > now;

    // From the fifth failure on, the account is locked for 30s, then 60, 120 and so on, up to half an hour.
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= LockoutThreshold)
        {
            // capped so the shift below cannot overflow
            var steps = Math.Min(FailedLoginAttempts - LockoutThreshold, 20);
            var seconds = Math.Min(MaxLockoutSeconds, BaseLockoutSeconds * (1 << steps));

            LockoutUntil = now.AddSeconds(seconds);
        }
    }

    public void ResetLoginFailures()
    {
        FailedLoginAttempts = 0;
        LockoutUntil = null;
    }

    public void AddRole(Role role)
    {
        if (!_roles.Exists(held => held.Id == role.Id))
        {
            _roles.Add(role);
        }
    }

    public void RemoveRole(Role role) => _roles.RemoveAll(held => held.Id == role.Id);

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        RotateSecurityStamp();
    }

    // Same password, new cost. Rotating here would sign the person out everywhere for logging in.
    public void UpgradePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void SetTotpSecret(string encryptedSecret)
    {
        EncryptedTotpSecret = encryptedSecret;
        TotpEnabled = false;
        RotateSecurityStamp();
    }

    public void ConfirmTotp()
    {
        TotpEnabled = true;
        RotateSecurityStamp();
    }

    public void DisableTotp()
    {
        EncryptedTotpSecret = null;
        TotpEnabled = false;
        RotateSecurityStamp();
    }

    public void EndAllSessions() => RotateSecurityStamp();

    // For key rotation: 1 when a secret was rewrapped, so a job can count them; TotpEnabled stays as it is.
    public int Reencrypt(Func<string, string> transform)
    {
        if (EncryptedTotpSecret is not { } secret)
        {
            return 0;
        }

        EncryptedTotpSecret = transform(secret);

        return 1;
    }

    public void SetOwner(bool isOwner) => IsOwner = isOwner;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}
