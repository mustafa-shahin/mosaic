using Microsoft.EntityFrameworkCore;
using Mosaic.Api.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Registration;

public sealed class RegisterUser(
    ManagementDbContext database,
    RegisterRequestValidator validator,
    PasswordHasher passwordHasher,
    SetupTokenFile setupToken,
    TimeProvider clock)
{
    public async Task HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var settings = await database.InstanceSettings.SingleOrDefaultAsync(cancellationToken);

        // refused before the request is read, so a closed instance gives every address the same answer
        if (settings is not null)
        {
            throw new NotPermittedException(ErrorCodes.RegistrationClosed);
        }

        var trimmed = Trim(request);

        // checked before the rules, so a stranger without the token gets no field errors and never reaches the hasher
        RequireSetupToken(trimmed.SetupToken);

        validator.Validate(trimmed).ThrowIfInvalid();

        var now = clock.GetUtcNow();
        var passwordHash = await passwordHasher.HashAsync(trimmed.Password!, cancellationToken);

        // spent before the save, so a crash in between cannot leave a token that works after the next restore
        setupToken.Discard();

        // the owner and the claim are one save, so a second claim fails without leaving half an account
        database.Users.Add(User.CreateOwner(trimmed.Email!, trimmed.FirstName!, trimmed.LastName!, passwordHash, now));
        database.InstanceSettings.Add(InstanceSettings.Claim(now));

        await database.SaveChangesAsync(cancellationToken);
    }

    private void RequireSetupToken(string? supplied)
    {
        var verdict = setupToken.Verify(supplied);

        if (verdict == SetupTokenResult.Missing)
        {
            throw new NotPermittedException(ErrorCodes.SetupTokenRequired);
        }

        if (verdict == SetupTokenResult.Invalid)
        {
            throw new NotPermittedException(ErrorCodes.SetupTokenInvalid);
        }
    }

    // the password is never trimmed, a space in it is a character the person chose
    private static RegisterRequest Trim(RegisterRequest request) => request with
    {
        Email = request.Email?.Trim(),
        FirstName = request.FirstName?.Trim(),
        LastName = request.LastName?.Trim(),
        SetupToken = request.SetupToken?.Trim(),
    };
}
