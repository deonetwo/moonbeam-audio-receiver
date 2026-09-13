namespace MoonbeamAudioReceiver.Models;

public sealed class AppSettings
{
    public bool AutoConnectPreferredDevice { get; set; } = true;
    public string? PreferredDeviceId { get; set; }
    public string? LastConnectedDeviceId { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
}

