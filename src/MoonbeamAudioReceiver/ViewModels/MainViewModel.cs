using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoonbeamAudioReceiver.Models;
using MoonbeamAudioReceiver.Services;

namespace MoonbeamAudioReceiver.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothReceiverService _bluetoothService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _durationTimer;
    private Stopwatch? _streamingStopwatch;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    [ObservableProperty]
    private string _statusText = "Scanning for Bluetooth devices...";

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _connectedDeviceName = string.Empty;

    [ObservableProperty]
    private string _streamingDurationText = "00:00:00";

    [ObservableProperty]
    private bool _hasDevices;

    [ObservableProperty]
    private bool _autoConnect;

    [ObservableProperty]
    private bool _minimizeToTray;

    public MainViewModel(IBluetoothReceiverService bluetoothService, ISettingsService settingsService)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;

        _autoConnect = _settingsService.Settings.AutoConnectPreferredDevice;
        _minimizeToTray = _settingsService.Settings.MinimizeToTrayOnClose;

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += OnDurationTimerTick;

        _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothService.DeviceRemoved += OnDeviceRemoved;
        _bluetoothService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public void Initialize()
    {
        _settingsService.Load();
        AutoConnect = _settingsService.Settings.AutoConnectPreferredDevice;
        MinimizeToTray = _settingsService.Settings.MinimizeToTrayOnClose;

        _bluetoothService.StartDiscovery();
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        _bluetoothService.StopDiscovery();
        Devices.Clear();
        HasDevices = false;
        StatusText = "Scanning for Bluetooth devices...";
        _bluetoothService.StartDiscovery();
    }

    [RelayCommand]
    private static void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch
        {
            // Ignore if settings uri fails to launch
        }
    }

    [RelayCommand]
    private async Task DisconnectCurrentAsync()
    {
        await _bluetoothService.DisconnectAsync();
    }

    partial void OnAutoConnectChanged(bool value)
    {
        _settingsService.Settings.AutoConnectPreferredDevice = value;
        _settingsService.Save();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settingsService.Settings.MinimizeToTrayOnClose = value;
        _settingsService.Save();
    }

    public void TogglePreferredDevice(string deviceId)
    {
        if (string.Equals(_settingsService.Settings.PreferredDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            // Deselect / Unset favorite
            _settingsService.Settings.PreferredDeviceId = null;
        }
        else
        {
            _settingsService.Settings.PreferredDeviceId = deviceId;
        }
        _settingsService.Save();

        // Update observable state across all devices in UI
        foreach (var dev in Devices)
        {
            dev.IsPreferred = string.Equals(dev.Id, _settingsService.Settings.PreferredDeviceId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task PairDeviceAsync(string deviceId)
    {
        StatusText = "Pairing device...";
        bool success = await _bluetoothService.PairAsync(deviceId);
        if (success)
        {
            StatusText = "Device paired successfully. Ready to connect.";
        }
        else
        {
            StatusText = "Pairing cancelled or failed.";
        }
    }

    private async Task ConnectDeviceAsync(string deviceId)
    {
        IsConnecting = true;
        StatusText = "Connecting...";

        try
        {
            await _bluetoothService.ConnectAsync(deviceId);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task DisconnectDeviceAsync()
    {
        await _bluetoothService.DisconnectAsync();
    }

    private void OnDeviceDiscovered(object? sender, BluetoothAudioDevice device)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            bool isPref = string.Equals(_settingsService.Settings.PreferredDeviceId, device.Id, StringComparison.OrdinalIgnoreCase);
            var existing = Devices.FirstOrDefault(d => string.Equals(d.Id, device.Id, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.UpdateFrom(device, isPref);
            }
            else
            {
                var vm = new DeviceItemViewModel(
                    device,
                    isPref,
                    ConnectDeviceAsync,
                    PairDeviceAsync,
                    DisconnectDeviceAsync,
                    TogglePreferredDevice);
                Devices.Add(vm);
            }

            HasDevices = Devices.Count > 0;
            if (!IsStreaming && !IsConnecting)
            {
                StatusText = $"{Devices.Count} device(s) found";
            }
        });
    }

    private void OnDeviceRemoved(object? sender, string deviceId)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = Devices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Devices.Remove(existing);
            }
            HasDevices = Devices.Count > 0;
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            switch (e.State)
            {
                case ReceiverConnectionState.Streaming:
                    IsStreaming = true;
                    IsConnecting = false;
                    ConnectedDeviceName = e.DeviceName;
                    StatusText = $"Receiving audio from {e.DeviceName}";
                    _streamingStopwatch = Stopwatch.StartNew();
                    _durationTimer.Start();
                    break;

                case ReceiverConnectionState.Connecting:
                    IsStreaming = false;
                    IsConnecting = true;
                    StatusText = $"Connecting to {e.DeviceName}...";
                    break;

                case ReceiverConnectionState.Disconnected:
                    IsStreaming = false;
                    IsConnecting = false;
                    ConnectedDeviceName = string.Empty;
                    StatusText = Devices.Count > 0 ? $"{Devices.Count} device(s) ready" : "Ready to connect";
                    _streamingStopwatch?.Stop();
                    _durationTimer.Stop();
                    StreamingDurationText = "00:00:00";
                    break;

                case ReceiverConnectionState.Failed:
                    IsStreaming = false;
                    IsConnecting = false;
                    StatusText = e.ErrorMessage ?? "Connection failed";
                    _streamingStopwatch?.Stop();
                    _durationTimer.Stop();
                    break;
            }

            foreach (var dev in Devices)
            {
                dev.IsConnected = IsStreaming && string.Equals(dev.Id, e.DeviceId, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    private void OnDurationTimerTick(object? sender, EventArgs e)
    {
        if (_streamingStopwatch != null)
        {
            var elapsed = _streamingStopwatch.Elapsed;
            StreamingDurationText = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }
    }
}
