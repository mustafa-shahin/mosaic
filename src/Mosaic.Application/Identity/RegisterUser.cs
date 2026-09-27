using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Application.Validation;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Domain.Errors;
using Mosaic.Domain.Identity;
using Mosaic.Domain.Settings;

namespace Mosaic.Application.Identity;

public sealed class RegisterUser(
    IManagementDbContext database,
    RegisterRequestValidator validator,
    PasswordHasher passwordHasher,
    ISetupToken setupToken,
    TimeProvider clock)
{
    public async Task<RegisterResult> HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var settings = await database.InstanceSettings.SingleOrDefaultAsync(cancellationToken);

        // refused before the request is read, so a closed instance gives every address the same answer
        if (settings is { RegistrationPolicy: RegistrationPolicy.Closed })
        {
            throw new NotPermittedException(ErrorCodes.RegistrationClosed);
        }

        var trimmed = Trim(request);

        // checked before the rules run, so a stranger on an unclaimed instance never reaches the hasher
        if (settings is null)
        {
            RequireSetupToken(trimmed.SetupToken);
        }

        var result = validator.Validate(trimmed);

        // asked only when the address itself passed, so a taken address and a short password come back together
        if (result.Errors.TrueForAll(failure => failure.PropertyName != nameof(RegisterRequest.Email))
            && await IsTakenAsync(trimmed.Email!, cancellationToken))
        {
            result.Errors.Add(new ValidationFailure(nameof(RegisterRequest.Email), string.Empty)
            {
                ErrorCode = ErrorCodes.AlreadyTaken,
            });
        }

        result.ThrowIfInvalid();

        var now = clock.GetUtcNow();
        var passwordHash = await passwordHasher.HashAsync(trimmed.Password!, cancellationToken);

        // the owner and the claim are one save, so a second claim fails without leaving half an account
        if (settings is null)
        {
            // spent before the save, so a crash in between cannot leave a token that works after the next restore
            setupToken.Discard();

            database.Users.Add(User.CreateOwner(trimmed.Email!, trimmed.FirstName!, trimmed.LastName!, passwordHash, now));
            database.InstanceSettings.Add(InstanceSettings.Claim(now));
        }
        else
        {
            database.Users.Add(User.Register(trimmed.Email!, trimmed.FirstName!, trimmed.LastName!, passwordHash, null, now));
        }

        await database.SaveChangesAsync(cancellationToken);

        return new RegisterResult { Outcome = settings is null ? RegistrationOutcome.Owner : RegistrationOutcome.Pending };
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

    // deleted accounts count too, because the unique index on email is not filtered
    private Task<bool> IsTakenAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = User.Normalize(email);

        return database.Users.IgnoreQueryFilters().AnyAsync(user => user.Email == normalized, cancellationToken);
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
