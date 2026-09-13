using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using MoonbeamAudioReceiver.Models;

namespace MoonbeamAudioReceiver.Services;

public enum ReceiverConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Streaming,
    Failed
}

public sealed record ConnectionStateChangedEventArgs(
    string DeviceId,
    string DeviceName,
    ReceiverConnectionState State,
    string? ErrorMessage = null);

public interface IBluetoothReceiverService : IAsyncDisposable, IDisposable
{
    ReceiverConnectionState CurrentState { get; }
    BluetoothAudioDevice? ConnectedDevice { get; }
    IReadOnlyList<BluetoothAudioDevice> DiscoveredDevices { get; }

    event EventHandler<BluetoothAudioDevice>? DeviceDiscovered;
    event EventHandler<string>? DeviceRemoved;
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    void StartDiscovery();
    void StopDiscovery();
    Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> PairAsync(string deviceId, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public sealed class BluetoothReceiverService : IBluetoothReceiverService
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, BluetoothAudioDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private DeviceWatcher? _audioWatcher;
    private DeviceWatcher? _unpairedWatcher;
    private AudioPlaybackConnection? _activeConnection;
    private string? _activeDeviceId;
    private ReceiverConnectionState _currentState = ReceiverConnectionState.Disconnected;

    public ReceiverConnectionState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                var device = ConnectedDevice;
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    device?.Id ?? string.Empty,
                    device?.Name ?? "Unknown Device",
                    value));
            }
        }
    }

    public BluetoothAudioDevice? ConnectedDevice
    {
        get
        {
            if (_activeDeviceId != null && _devices.TryGetValue(_activeDeviceId, out var device))
            {
                return device;
            }
            return null;
        }
    }

    public IReadOnlyList<BluetoothAudioDevice> DiscoveredDevices
    {
        get
        {
            lock (_lock)
            {
                return _devices.Values.ToList();
            }
        }
    }

    public event EventHandler<BluetoothAudioDevice>? DeviceDiscovered;
    public event EventHandler<string>? DeviceRemoved;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public BluetoothReceiverService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void StartDiscovery()
    {
        StopDiscovery();

        try
        {
            // 1. Watch for paired A2DP audio playback sources
            string audioSelector = AudioPlaybackConnection.GetDeviceSelector();
            _audioWatcher = DeviceInformation.CreateWatcher(audioSelector);
            _audioWatcher.Added += (s, e) => ProcessDevice(e, isAudioSinkCapable: true);
            _audioWatcher.Updated += OnDeviceUpdated;
            _audioWatcher.Removed += OnDeviceRemoved;
            _audioWatcher.Start();

            // 2. Watch for unpaired Bluetooth devices ready to pair
            string unpairedSelector = BluetoothDevice.GetDeviceSelectorFromPairingState(false);
            _unpairedWatcher = DeviceInformation.CreateWatcher(unpairedSelector);
            _unpairedWatcher.Added += (s, e) => ProcessDevice(e, isAudioSinkCapable: false);
            _unpairedWatcher.Updated += OnDeviceUpdated;
            _unpairedWatcher.Removed += OnDeviceRemoved;
            _unpairedWatcher.Start();
        }
        catch (Exception ex)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                string.Empty,
                string.Empty,
                ReceiverConnectionState.Failed,
                $"Failed to start device discovery: {ex.Message}"));
        }
    }

    public void StopDiscovery()
    {
        StopWatcher(ref _audioWatcher);
        StopWatcher(ref _unpairedWatcher);
    }

    private static void StopWatcher(ref DeviceWatcher? watcher)
    {
        if (watcher == null) return;
        try
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                watcher.Stop();
            }
        }
        catch
        {
            // Suppress stop error during teardown
        }
        finally
        {
            watcher = null;
        }
    }

    private void ProcessDevice(DeviceInformation args, bool isAudioSinkCapable)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
        {
            return;
        }

        var device = new BluetoothAudioDevice(
            args.Id,
            args.Name,
            IsPaired: args.Pairing.IsPaired || isAudioSinkCapable,
            IsConnected: string.Equals(_activeDeviceId, args.Id, StringComparison.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow);

        lock (_lock)
        {
            _devices[args.Id] = device;
        }

        DeviceDiscovered?.Invoke(this, device);

        // Target for auto-connect: either explicit preferred device, or last connected device
        string? targetAutoConnectId = _settingsService.Settings.PreferredDeviceId ?? _settingsService.Settings.LastConnectedDeviceId;

        if (device.IsPaired &&
            _settingsService.Settings.AutoConnectPreferredDevice &&
            !string.IsNullOrWhiteSpace(targetAutoConnectId) &&
            string.Equals(targetAutoConnectId, args.Id, StringComparison.OrdinalIgnoreCase) &&
            _activeConnection == null)
        {
            _ = Task.Run(() => ConnectAsync(args.Id));
        }
    }

    public async Task<bool> PairAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        try
        {
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
            if (deviceInfo == null)
            {
                return false;
            }

            if (deviceInfo.Pairing.IsPaired)
            {
                return true;
            }

            // Initiate custom pairing handler to accept confirmations
            var customPairing = deviceInfo.Pairing.Custom;
            customPairing.PairingRequested += (s, e) =>
            {
                e.Accept();
            };

            var result = await customPairing.PairAsync(
                DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ProvidePin | DevicePairingKinds.DisplayPin);

            if (result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired)
            {
                lock (_lock)
                {
                    if (_devices.TryGetValue(deviceId, out var existing))
                    {
                        var updated = existing with { IsPaired = true };
                        _devices[deviceId] = updated;
                        DeviceDiscovered?.Invoke(this, updated);
                    }
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                deviceId,
                GetDeviceName(deviceId),
                ReceiverConnectionState.Failed,
                $"Pairing failed: {ex.Message}"));
            return false;
        }
    }

    public async Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        if (_activeConnection != null)
        {
            await DisconnectAsync();
        }

        CurrentState = ReceiverConnectionState.Connecting;

        try
        {
            var connection = AudioPlaybackConnection.TryCreateFromId(deviceId);
            if (connection == null)
            {
                CurrentState = ReceiverConnectionState.Failed;
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    deviceId,
                    GetDeviceName(deviceId),
                    ReceiverConnectionState.Failed,
                    "Selected device is not ready for audio streaming. Ensure it is paired."));
                return false;
            }

            _activeConnection = connection;
            _activeDeviceId = deviceId;

            connection.StateChanged += OnConnectionStateChanged;

            // StartAsync registers intent with Windows audio engine
            connection.Start();

            // OpenAsync establishes active A2DP sink audio stream
            var result = await connection.OpenAsync();
            if (result.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                CurrentState = ReceiverConnectionState.Streaming;

                // Save last connected device without overwriting explicit user favorites
                _settingsService.Settings.LastConnectedDeviceId = deviceId;
                _settingsService.Save();

                lock (_lock)
                {
                    if (_devices.TryGetValue(deviceId, out var existing))
                    {
                        _devices[deviceId] = existing with { IsConnected = true, IsPaired = true };
                    }
                }

                return true;
            }
            else
            {
                CurrentState = ReceiverConnectionState.Failed;
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    deviceId,
                    GetDeviceName(deviceId),
                    ReceiverConnectionState.Failed,
                    $"Connection failed with status: {result.Status}"));

                await DisconnectAsync();
                return false;
            }
        }
        catch (Exception ex)
        {
            CurrentState = ReceiverConnectionState.Failed;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                deviceId,
                GetDeviceName(deviceId),
                ReceiverConnectionState.Failed,
                $"Connection error: {ex.Message}"));

            await DisconnectAsync();
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (_activeConnection != null)
            {
                _activeConnection.StateChanged -= OnConnectionStateChanged;
                _activeConnection.Dispose();
                _activeConnection = null;
            }

            if (_activeDeviceId != null)
            {
                lock (_lock)
                {
                    if (_devices.TryGetValue(_activeDeviceId, out var dev))
                    {
                        _devices[_activeDeviceId] = dev with { IsConnected = false };
                    }
                }
                _activeDeviceId = null;
            }

            CurrentState = ReceiverConnectionState.Disconnected;
        }
        catch
        {
            CurrentState = ReceiverConnectionState.Disconnected;
        }

        return Task.CompletedTask;
    }

    private void OnConnectionStateChanged(AudioPlaybackConnection sender, object args)
    {
        switch (sender.State)
        {
            case AudioPlaybackConnectionState.Opened:
                CurrentState = ReceiverConnectionState.Streaming;
                break;
            case AudioPlaybackConnectionState.Closed:
                CurrentState = ReceiverConnectionState.Disconnected;
                break;
        }
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        lock (_lock)
        {
            if (_devices.TryGetValue(args.Id, out var existing))
            {
                var updated = existing with { LastSeen = DateTimeOffset.UtcNow };
                _devices[args.Id] = updated;
                DeviceDiscovered?.Invoke(this, updated);
            }
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        lock (_lock)
        {
            _devices.Remove(args.Id);
        }

        if (string.Equals(_activeDeviceId, args.Id, StringComparison.OrdinalIgnoreCase))
        {
            _ = DisconnectAsync();
        }

        DeviceRemoved?.Invoke(this, args.Id);
    }

    private string GetDeviceName(string deviceId)
    {
        lock (_lock)
        {
            if (_devices.TryGetValue(deviceId, out var dev))
            {
                return dev.Name;
            }
        }
        return "Unknown Device";
    }

    public void Dispose()
    {
        StopDiscovery();
        _activeConnection?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
