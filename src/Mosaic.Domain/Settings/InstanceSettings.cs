using Mosaic.Domain.Common;

namespace Mosaic.Domain.Settings;

// the installation's one row; writing it is the first owner's claim
public sealed class InstanceSettings : Timestamped
{
    // every claim writes this key, so a second claim collides with the first
    public const int SingleRowId = 1;

    private InstanceSettings()
    {
    }

    public int Id { get; private set; }

    public RegistrationPolicy RegistrationPolicy { get; private set; }

    public static InstanceSettings Claim(DateTimeOffset now) => new()
    {
        Id = SingleRowId,
        RegistrationPolicy = RegistrationPolicy.Closed,
        CreatedAt = now,
    };
}
