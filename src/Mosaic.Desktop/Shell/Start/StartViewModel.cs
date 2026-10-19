using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Shell.Start;

// only the server knows whether the instance has an owner, so the first screen waits for its answer
public sealed partial class StartViewModel(AuthApi auth, Localizer strings) : ObservableObject
{
    public Localizer Strings { get; } = strings;

    public string Server { get; } = auth.BaseAddress?.Authority ?? string.Empty;

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

    public event EventHandler<RulesResult>? Answered;

    // Execute while one runs would cancel the check in flight and start another
    public void CheckIfIdle()
    {
        if (!CheckCommand.IsRunning)
        {
            CheckCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        ErrorCode = null;
        ErrorParams = null;

        var response = await auth.GetRulesAsync(cancellationToken);

        if (response.Success && response.Data is { } rules)
        {
            Answered?.Invoke(this, rules);
            return;
        }

        ErrorCode = response.Error?.Code ?? DesktopErrorCodes.UnexpectedResponse;
        ErrorParams = response.Error?.Params;
    }
}
