using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mosaic.Contracts;
using Mosaic.Desktop.Services;

namespace Mosaic.Desktop.ViewModels;

public sealed partial class HealthViewModel(ApiClient api) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecking))]
    [NotifyPropertyChangedFor(nameof(IsReachable))]
    [NotifyPropertyChangedFor(nameof(IsUnreachable))]
    [NotifyPropertyChangedFor(nameof(IsIncompatible))]
    private ConnectionState _state = ConnectionState.Checking;

    [ObservableProperty]
    private string? _service;

    [ObservableProperty]
    private DateTimeOffset? _serverTimeUtc;

    [ObservableProperty]
    private string? _errorCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractSummary))]
    private string? _serverContract;

    public bool IsChecking => State == ConnectionState.Checking;
    public bool IsReachable => State == ConnectionState.Reachable;
    public bool IsUnreachable => State == ConnectionState.Unreachable;
    public bool IsIncompatible => State == ConnectionState.Incompatible;

    public string ContractSummary =>
        $"This desktop speaks {ApiContract.Version}. The server speaks {ServerContract ?? "an older version"}.";

    [RelayCommand]
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        State = ConnectionState.Checking;
        ErrorCode = null;

        var response = await api.GetAsync<HealthResult>("health", cancellationToken);

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
        State = ConnectionState.Unreachable;
    }
}
