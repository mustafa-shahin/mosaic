using Microsoft.EntityFrameworkCore;
using Mosaic.Application.Persistence;
using Mosaic.Application.Security;
using Mosaic.Contracts.Identity;

namespace Mosaic.Application.Identity;

public sealed class GetRules(IManagementDbContext database, ISetupToken setupToken)
{
    public async Task<RulesResult> HandleAsync(CancellationToken cancellationToken)
    {
        var settings = await database.InstanceSettings.SingleOrDefaultAsync(cancellationToken);

        // the first-run screen asks for the token before it sends anything, so the file has to exist by then
        if (settings is null)
        {
            setupToken.Prepare();
        }

        return new RulesResult
        {
            Registration = settings is null ? RegistrationState.Unclaimed : RegistrationState.Closed,
            Password = new PasswordRule
            {
                MinLength = UserLimits.PasswordMinLength,
                MaxLength = UserLimits.PasswordMaxLength,
            },
        };
    }
}
