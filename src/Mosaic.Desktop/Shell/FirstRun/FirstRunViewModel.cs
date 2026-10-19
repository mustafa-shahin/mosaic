using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.SignIn;

namespace Mosaic.Desktop.Shell.FirstRun;

public sealed partial class FirstRunViewModel : ObservableObject
{
    private readonly AuthApi _auth;

    // keyed by the name the server gives a field, which is its name in the request's JSON
    private readonly Dictionary<string, FormField> _byServerName;

    public FirstRunViewModel(AuthApi auth, Localizer strings, PasswordRule rule)
    {
        _auth = auth;
        Strings = strings;
        Server = auth.BaseAddress?.Authority ?? string.Empty;
        PasswordGuidance = strings.Translate("first_run.password_guidance", new Dictionary<string, string>
        {
            ["min"] = rule.MinLength.ToString(CultureInfo.InvariantCulture)
        });

        _byServerName = new Dictionary<string, FormField>(StringComparer.Ordinal)
        {
            [ServerName(nameof(RegisterRequest.SetupToken))] = SetupToken,
            [ServerName(nameof(RegisterRequest.Email))] = Email,
            [ServerName(nameof(RegisterRequest.FirstName))] = FirstName,
            [ServerName(nameof(RegisterRequest.LastName))] = LastName,
            [ServerName(nameof(RegisterRequest.Password))] = Password,
        };
    }

    public Localizer Strings { get; }

    // the server the token and the owner's password go to
    public string Server { get; }

    // the server's own number, so the guidance and a refusal never disagree
    public string PasswordGuidance { get; }

    public FormField SetupToken { get; } = new();

    public FormField Email { get; } = new();

    public FormField FirstName { get; } = new();

    public FormField LastName { get; } = new();

    public FormField Password { get; } = new();

    public FormField RepeatPassword { get; } = new();

    // a refusal that belongs to no field
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

    public event EventHandler<SignInHandover>? HandedOver;

    // the view moves the focus to the first field that was refused
    public event EventHandler? Refused;

    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        ClearRefusals();

        // only this screen sees both entries, and no reset exists for an owner who mistyped
        if (!string.Equals(Password.Value, RepeatPassword.Value, StringComparison.Ordinal))
        {
            RepeatPassword.Error = Strings.TranslateCode(DesktopErrorCodes.PasswordsDiffer);
            Refused?.Invoke(this, EventArgs.Empty);
            return;
        }

        // as typed: the server trims the address, the names and the token, and never the password
        var registered = await _auth.RegisterAsync(
            new RegisterRequest
            {
                Email = Email.Value,
                FirstName = FirstName.Value,
                LastName = LastName.Value,
                Password = Password.Value,
                SetupToken = SetupToken.Value,
            },
            cancellationToken);

        // most often this person's earlier attempt claimed the instance and its answer was lost, so they sign in instead
        if (registered.Error?.Code == ErrorCodes.RegistrationClosed)
        {
            HandedOver?.Invoke(this, new SignInHandover(Email.Value, Note: null, registered.Error.Code, registered.Error.Params));
            return;
        }

        if (!registered.Success)
        {
            ShowRefusal(registered.Error);
            Refused?.Invoke(this, EventArgs.Empty);
            return;
        }

        // register issues no session, so the owner signs in with what was just typed
        var signedIn = await _auth.SignInAsync(new SignInRequest { Email = Email.Value, Password = Password.Value }, cancellationToken);

        if (signedIn.Success && signedIn.Data is not null)
        {
            SignedIn?.Invoke(this, signedIn.Data);
            return;
        }

        HandedOver?.Invoke(this, new SignInHandover(
            Email.Value, SignInNote.AccountReady, signedIn.Error?.Code ?? DesktopErrorCodes.UnexpectedResponse, signedIn.Error?.Params));
    }

    private void ClearRefusals()
    {
        ErrorCode = null;
        ErrorParams = null;

        foreach (var field in new[] { SetupToken, Email, FirstName, LastName, Password, RepeatPassword })
        {
            field.Error = null;
        }
    }

    private void ShowRefusal(ApiError? error)
    {
        if (error is null)
        {
            ErrorCode = DesktopErrorCodes.UnexpectedResponse;
            return;
        }

        // the server refuses the token for the whole request, but the person fixes it in its field
        if (error.Code is ErrorCodes.SetupTokenRequired or ErrorCodes.SetupTokenInvalid)
        {
            SetupToken.Error = Strings.TranslateCode(error.Code, error.Params);
            return;
        }

        if (error.FieldErrors is not { Count: > 0 } fieldErrors)
        {
            ErrorCode = error.Code;
            ErrorParams = error.Params;
            return;
        }

        foreach (var (name, failures) in fieldErrors)
        {
            if (failures is not [var failure, ..])
            {
                continue;
            }

            if (_byServerName.TryGetValue(name, out var field))
            {
                field.Error = Strings.TranslateCode(failure.Code, failure.Params);
            }
            else
            {
                // a field this screen does not show, so the person is still told that something is wrong
                ErrorCode = error.Code;
                ErrorParams = error.Params;
            }
        }
    }

    // the server's adapter names a field with this policy
    private static string ServerName(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);
}
