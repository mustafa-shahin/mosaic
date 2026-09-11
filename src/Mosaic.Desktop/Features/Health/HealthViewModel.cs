using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts;
using Mosaic.Desktop.Controls;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.Features.Health;

public sealed partial class HealthViewModel : ObservableObject, IDisposable
{
    private readonly ApiClient _api;

    public HealthViewModel(ApiClient api, Localizer strings)
    {
        _api = api;
        Strings = strings;
    }

    public Localizer Strings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecking))]
    [NotifyPropertyChangedFor(nameof(IsReachable))]
    [NotifyPropertyChangedFor(nameof(IsUnreachable))]
    [NotifyPropertyChangedFor(nameof(IsIncompatible))]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ConnectionState State { get; set; } = ConnectionState.Checking;

    [ObservableProperty]
    public partial string? Service { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? ServerTimeUtc { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial string? ErrorCode { get; set; }

    // placeholder values from the server, see Localizer.TranslateCode
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial IReadOnlyDictionary<string, string>? ErrorParams { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractSummary))]
    public partial string? ServerContract { get; set; }

    public bool IsChecking => State == ConnectionState.Checking;
    public bool IsReachable => State == ConnectionState.Reachable;
    public bool IsUnreachable => State == ConnectionState.Unreachable;
    public bool IsIncompatible => State == ConnectionState.Incompatible;

    public StatusLevel Status => State switch
    {
        ConnectionState.Reachable => StatusLevel.Ok,
        ConnectionState.Unreachable => StatusLevel.Bad,
        ConnectionState.Incompatible => StatusLevel.Warn,
        _ => StatusLevel.Info
    };

    public string StatusText => State switch
    {
        ConnectionState.Reachable => Strings.Translate("health.reachable"),
        ConnectionState.Unreachable => Strings.Translate("health.unreachable"),
        ConnectionState.Incompatible => Strings.Translate("health.incompatible"),
        _ => Strings.Translate("health.checking")
    };

    public string? ErrorMessage => ErrorCode is null ? null : Strings.TranslateCode(ErrorCode, ErrorParams);

    public string ContractSummary => Strings.Translate("health.contract_mismatch", new Dictionary<string, string>
    {
        ["desktop"] = ApiContract.Version,
        ["server"] = ServerContract ?? Strings.Translate("health.contract_unknown")
    });

    // The shell disposes the screen it leaves, so a check still in flight is cancelled here rather
    // than left to finish writing into a view model nobody is bound to.
    public void Dispose() => CheckCommand.Cancel();

    // AsyncRelayCommand cancels a running execution when Execute is called again, and a second check
    // would restart the spinner for no new information. Hence the IsRunning check.
    public void CheckIfIdle()
    {
        if (!CheckCommand.IsRunning)
        {
            CheckCommand.Execute(null);
        }
    }

    [RelayCommand]
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        State = ConnectionState.Checking;
        ErrorCode = null;
        ErrorParams = null;

        try
        {
            var response = await _api.GetAsync<HealthResult>("health", cancellationToken);

            if (response.Success && response.Data is { } health)
            {
                Service = health.Service;
                ServerTimeUtc = health.ServerTimeUtc;
                ServerContract = health.ApiContract;
                State = ApiContract.IsCompatible(health.ApiContract)
                    ? ConnectionState.Reachable
                    : ConnectionState.Incompatible;
                return;
            }

            Service = null;
            ServerTimeUtc = null;
            ServerContract = null;
            ErrorCode = response.Error?.Code;
            ErrorParams = response.Error?.Params;
            State = ConnectionState.Unreachable;
        }
        // the screen was left mid-check; there is nothing to show and nobody left to show it to
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
