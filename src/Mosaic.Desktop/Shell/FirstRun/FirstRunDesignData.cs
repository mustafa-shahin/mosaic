using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Shell.FirstRun;

// For the previewer only. A refused token, so a field error and the password guidance show together.
internal static class FirstRunDesignData
{
    public static FirstRunViewModel Refused { get; } = Build();

    private static FirstRunViewModel Build()
    {
        var viewModel = new FirstRunViewModel(
            new AuthApi(DesignContext.Api), DesignContext.Strings, new PasswordRule { MinLength = 15, MaxLength = 128 });

        viewModel.SetupToken.Value = "pasted-from-somewhere-else";
        viewModel.SetupToken.Error = DesignContext.Strings.TranslateCode(ErrorCodes.SetupTokenInvalid);
        viewModel.Email.Value = "user@example.com";
        viewModel.FirstName.Value = "Ada";
        viewModel.LastName.Value = "Lovelace";

        return viewModel;
    }
}
