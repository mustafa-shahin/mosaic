using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Identity;

public sealed class SignInUser(IManagementDbContext database, PasswordHasher passwordHasher, TimeProvider clock)
{
    public async Task<SignInResult> HandleAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        var email = User.Normalize(request.Email ?? string.Empty);
        var password = request.Password ?? string.Empty;

        // the query filter hides a deleted account, so it is refused like an address nobody has
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        // runs for an unknown address too, so the time taken does not tell the two apart
        var verdict = await passwordHasher.VerifyAsync(password, user?.PasswordHash, cancellationToken);
        var now = clock.GetUtcNow();

        // checked after the hash, and the right password counts too, so every refusal does the same work
        if (user is null || verdict == PasswordVerificationResult.Failed || user.IsLockedOut(now))
        {
            if (user is not null)
            {
                user.RegisterFailedLogin(now);

                await database.SaveChangesAsync(cancellationToken);
            }

            throw new NotPermittedException(ErrorCodes.InvalidCredentials);
        }

        // reached only with the right password, so naming the reason tells the caller nothing new
        if (!user.IsActive)
        {
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

        await database.SaveChangesAsync(cancellationToken);

        return new SignInResult
        {
            Token = token,
            Session = new SessionResult
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
            },
        };
    }
}
