using Microsoft.EntityFrameworkCore;
using Mosaic.Contracts;
using Mosaic.Contracts.Auditing;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Auditing;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Sessions;

public sealed class SignInUser(ManagementDbContext database, PasswordHasher passwordHasher, TimeProvider clock)
{
    public async Task<SignInResult> HandleAsync(
        SignInRequest request, AuditOrigin auditOrigin, CancellationToken cancellationToken)
    {
        var email = User.Normalize(request.Email ?? string.Empty);
        var password = request.Password ?? string.Empty;

        // the query filter hides a deleted account, so it is refused like an address nobody has
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        var auditTarget = user is null ? null : new AuditTarget(AuditTargets.User, user.Id, user.FullName);

        // runs for an unknown address too, so at today's cost the hash does not tell the two apart
        var verdict = await passwordHasher.VerifyAsync(password, user?.PasswordHash, cancellationToken);
        var now = clock.GetUtcNow();

        // checked after the hash, and the right password counts too, so the time does not show a lockout or a right guess
        if (user is null || verdict == PasswordVerificationResult.Failed || user.IsLockedOut(now))
        {
            var errorCode = user is not null && user.IsLockedOut(now) ? ErrorCodes.AccountLocked : ErrorCodes.InvalidCredentials;

            user?.RegisterFailedLogin(now);

            var failedAuditEntry = AuditEntry.Failed(
                AuditActions.SignIn, errorCode, actor: null, auditTarget, auditOrigin, now);

            await database.SaveChangesAsync(failedAuditEntry, cancellationToken);

            throw new NotPermittedException(ErrorCodes.InvalidCredentials);
        }

        var auditActor = new AuditActor(user.Id, user.FullName, user.Email);

        // reached only with the right password, so the reason is shown only to someone who knows the password
        if (!user.IsActive)
        {
            var failedAuditEntry = AuditEntry.Failed(
                AuditActions.SignIn, ErrorCodes.AccountInactive, auditActor, auditTarget, auditOrigin, now);

            await database.SaveChangesAsync(failedAuditEntry, cancellationToken);

            throw new NotPermittedException(ErrorCodes.AccountInactive);
        }

        if (verdict == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(await passwordHasher.HashAsync(password, cancellationToken));
        }

        user.ResetLoginFailures();
        user.RecordLogin(now);

        // a sign-in on another machine adds its own row and leaves the others alone
        var (token, hash) = SessionTokenFormat.Create();

        database.SessionTokens.Add(SessionToken.Issue(user, hash, now));

        var auditRow = AuditEntry.Succeeded(AuditActions.SignIn, auditActor, auditTarget, auditOrigin, now);

        await database.SaveChangesAsync(auditRow, cancellationToken);

        return new SignInResult
        {
            Token = token,
            Session = UserMapper.ToSessionResult(user),
        };
    }
}
