using MoonbeamAudioReceiver.Models;
using MoonbeamAudioReceiver.Services;
using Xunit;

namespace MoonbeamAudioReceiver.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreSensible()
    {
        var settings = new AppSettings();

        Assert.True(settings.AutoConnectPreferredDevice);
        Assert.True(settings.MinimizeToTrayOnClose);
        Assert.Null(settings.PreferredDeviceId);
        Assert.Null(settings.LastConnectedDeviceId);
    }

    [Fact]
    public void SettingsService_CanInstantiateAndModifySettings()
    {
        var service = new SettingsService();
        service.Settings.AutoConnectPreferredDevice = false;
        service.Settings.PreferredDeviceId = "BluetoothDevice#123";
        service.Settings.LastConnectedDeviceId = "BluetoothDevice#456";

        Assert.False(service.Settings.AutoConnectPreferredDevice);
        Assert.Equal("BluetoothDevice#123", service.Settings.PreferredDeviceId);
        Assert.Equal("BluetoothDevice#456", service.Settings.LastConnectedDeviceId);
    }

    [Fact]
    public void SettingsService_PreferredDeviceCanBeToggled()
    {
        var service = new SettingsService();
        service.Settings.PreferredDeviceId = "Device#1";
        Assert.Equal("Device#1", service.Settings.PreferredDeviceId);

        // Toggle off
        service.Settings.PreferredDeviceId = null;
        Assert.Null(service.Settings.PreferredDeviceId);
    }
}
