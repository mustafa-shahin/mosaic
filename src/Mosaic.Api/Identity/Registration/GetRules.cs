using Microsoft.EntityFrameworkCore;
using Mosaic.Contracts.Identity;
using Mosaic.Persistence;

namespace Mosaic.Api.Identity.Registration;

public sealed class GetRules(ManagementDbContext database, SetupTokenFile setupToken)
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
                MinLength = PasswordPolicy.MinLength,
                MaxLength = PasswordPolicy.MaxLength,
            },
        };
    }
}
