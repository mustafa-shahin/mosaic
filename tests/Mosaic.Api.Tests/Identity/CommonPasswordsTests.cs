using Mosaic.Api.Identity;

namespace Mosaic.Api.Tests.Identity;

public sealed class CommonPasswordsTests
{
    [Fact]
    public void AnEntryOfTheEmbeddedList_IsCommon()
    {
        Assert.True(CommonPasswords.IsCommon("passwordpassword", null, null, null));
    }

    [Fact]
    public void TheLicence_IsCopiedNextToTheDll()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Identity", "common-passwords.LICENSE.txt")));
    }
}
