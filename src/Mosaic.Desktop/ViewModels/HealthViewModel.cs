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
    private ConnectionState _state = ConnectionState.Checking;

    [ObservableProperty]
    private string? _service;

    [ObservableProperty]
    private DateTimeOffset? _serverTimeUtc;

    [ObservableProperty]
    private string? _errorCode;

    public bool IsChecking => State == ConnectionState.Checking;
    public bool IsReachable => State == ConnectionState.Reachable;
    public bool IsUnreachable => State == ConnectionState.Unreachable;

    [RelayCommand]
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        State = ConnectionState.Checking;
        ErrorCode = null;

        var response = await api.GetAsync<HealthResult>("/health", cancellationToken);

        if (response.Success && response.Data is { } health)
        {
            Service = health.Service;
            ServerTimeUtc = health.ServerTimeUtc;
            State = ConnectionState.Reachable;
            return;
        }

        Service = null;
        ServerTimeUtc = null;
        ErrorCode = response.Error?.Code;
        State = ConnectionState.Unreachable;
    }
}
