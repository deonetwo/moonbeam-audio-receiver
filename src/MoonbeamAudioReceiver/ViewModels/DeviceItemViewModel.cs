using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoonbeamAudioReceiver.Models;

namespace MoonbeamAudioReceiver.ViewModels;

public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly Func<string, Task> _connectAction;
    private readonly Func<string, Task> _pairAction;
    private readonly Func<Task> _disconnectAction;
    private readonly Action<string> _togglePreferredAction;

    public string Id { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isPaired;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isPairing;

    [ObservableProperty]
    private bool _isPreferred;

    public DeviceItemViewModel(
        BluetoothAudioDevice device,
        bool isPreferred,
        Func<string, Task> connectAction,
        Func<string, Task> pairAction,
        Func<Task> disconnectAction,
        Action<string> togglePreferredAction)
    {
        Id = device.Id;
        _name = device.Name;
        _isPaired = device.IsPaired;
        _isConnected = device.IsConnected;
        _isPreferred = isPreferred;
        _connectAction = connectAction;
        _pairAction = pairAction;
        _disconnectAction = disconnectAction;
        _togglePreferredAction = togglePreferredAction;
    }

    public void UpdateFrom(BluetoothAudioDevice device, bool isPreferred)
    {
        Name = device.Name;
        IsPaired = device.IsPaired;
        IsConnected = device.IsConnected;
        IsPreferred = isPreferred;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnecting || IsConnected || !IsPaired) return;

        IsConnecting = true;
        try
        {
            await _connectAction(Id);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task PairAsync()
    {
        if (IsPairing || IsPaired) return;

        IsPairing = true;
        try
        {
            await _pairAction(Id);
        }
        finally
        {
            IsPairing = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _disconnectAction();
    }

    [RelayCommand]
    private void TogglePreferred()
    {
        _togglePreferredAction(Id);
    }
}
