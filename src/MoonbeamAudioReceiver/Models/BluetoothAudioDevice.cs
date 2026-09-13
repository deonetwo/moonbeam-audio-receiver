namespace MoonbeamAudioReceiver.Models;

public record BluetoothAudioDevice(
    string Id,
    string Name,
    bool IsPaired,
    bool IsConnected,
    DateTimeOffset LastSeen);
