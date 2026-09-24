using Mosaic.Domain.Settings;

namespace Mosaic.Domain.Tests.Settings;

public sealed class InstanceSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Claim_StartsClosed()
    {
        Assert.Equal(RegistrationPolicy.Closed, InstanceSettings.Claim(Now).RegistrationPolicy);
    }

    [Fact]
    public void Claim_WritesTheOneRowAndWhenItHappened()
    {
        var settings = InstanceSettings.Claim(Now);

        Assert.Equal(InstanceSettings.SingleRowId, settings.Id);
        Assert.Equal(Now, settings.CreatedAt);
        Assert.Null(settings.UpdatedAt);
    }
}
