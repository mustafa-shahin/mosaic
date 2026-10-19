using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Shell.SignIn;

public sealed partial class SignInViewModel(AuthApi auth, Localizer strings) : ObservableObject
{
    public Localizer Strings { get; } = strings;

    // the server the password goes to, so a person with two installations can tell them apart
    public string Server { get; } = auth.BaseAddress?.Authority ?? string.Empty;

    public SignInNote? Note { get; init; }

    public bool HasNote => Note is not null;

    public string? NoteText => Note switch
    {
        SignInNote.AccountReady => Strings.Translate("sign_in.account_ready"),
        SignInNote.SessionEnded => Strings.Translate("sign_in.session_ended"),
        SignInNote.SignedOutEverywhere => Strings.Translate("sign_in.signed_out_everywhere"),
        _ => null
    };

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial string? ErrorCode { get; set; }

    // placeholder values from the server, see Localizer.TranslateCode
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial IReadOnlyDictionary<string, string>? ErrorParams { get; set; }

    public bool HasError => ErrorCode is not null;

    public string? ErrorMessage => ErrorCode is null ? null : Strings.TranslateCode(ErrorCode, ErrorParams);

    public event EventHandler<SignInResult>? SignedIn;

    // the view selects the password on this
    public event EventHandler? Refused;

    [RelayCommand]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorCode = null;
        ErrorParams = null;

        // as typed: the server trims and lower-cases the address, and never trims the password
        var response = await auth.SignInAsync(new SignInRequest { Email = Email, Password = Password }, cancellationToken);

        if (response.Success && response.Data is not null)
        {
            SignedIn?.Invoke(this, response.Data);
            return;
        }

        ErrorCode = response.Error?.Code ?? DesktopErrorCodes.UnexpectedResponse;
        ErrorParams = response.Error?.Params;

        Refused?.Invoke(this, EventArgs.Empty);
    }
}
