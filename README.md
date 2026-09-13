# Moonbeam Audio Receiver

A lightweight, modern Windows 11 Bluetooth Audio Receiver (**A2DP Sink**) desktop application built with **.NET 10 (LTS)**, C# 14, and WPF Fluent Design.

Moonbeam Audio Receiver turns your Windows PC into a wireless Bluetooth speaker, allowing you to stream music, podcasts, and audio directly from your smartphone, tablet, or laptop to your PC's speakers and headphones.

---

## Features

* **Native WinRT Audio Engine:** Uses Windows' built-in `Windows.Media.Audio.AudioPlaybackConnection` APIs for high-fidelity, low-latency audio decoding (SBC / AAC) rendered directly through WASAPI.
* **Dual Device Discovery:**
  * Detects paired audio playback devices ready to stream.
  * Discovers nearby unpaired Bluetooth transmitters broadcasting in pairing mode.
* **In-App Bluetooth Pairing:** Pair new Bluetooth devices directly from the app without opening Windows Settings.
* **Pinned Favorites & Auto-Reconnect:** Star your primary device to automatically reconnect whenever it is in range and powered on.
* **Active Streaming Indicator:** Real-time visual feedback with elapsed streaming timer and audio waveform animation.
* **System Tray Integration:** Runs quietly in the notification area with minimize-to-tray on close and double-click restore.
* **Windows 11 Fluent Design:** Built with Mica backdrop, responsive layout, dark/light theme awareness, and WCAG AA contrast compliance.

---

## Architecture Overview

```
moonbeam-audio-receiver/
├── MoonbeamAudioReceiver.slnx              # Solution file
├── src/
│   └── MoonbeamAudioReceiver/
│       ├── MoonbeamAudioReceiver.csproj   # Target: net10.0-windows10.0.22621.0
│       ├── app.manifest                   # Per-Monitor v2 DPI awareness
│       ├── App.xaml / App.xaml.cs         # Application entry and WPF-UI resource dictionaries
│       ├── Models/
│       │   ├── BluetoothAudioDevice.cs    # Device state representation
│       │   └── AppSettings.cs             # User preferences schema
│       ├── Services/
│       │   ├── ISettingsService.cs        # JSON settings persistence
│       │   ├── SettingsService.cs
│       │   ├── IBluetoothReceiverService.cs # WinRT AudioPlaybackConnection & DeviceWatcher
│       │   └── BluetoothReceiverService.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs           # Central reactive state and commands
│       │   └── DeviceItemViewModel.cs     # Per-device state and actions
│       ├── Converters/
│       │   └── ValueConverters.cs         # XAML value converters
│       └── Views/
│           ├── MainWindow.xaml            # Fluent UI view (Mica, Hero Card, Device List, Tray)
│           └── MainWindow.xaml.cs         # Window lifecycle and waveform storyboard controller
└── tests/
    └── MoonbeamAudioReceiver.Tests/
        ├── MoonbeamAudioReceiver.Tests.csproj # xUnit test suite (.NET 10)
        ├── ConvertersTests.cs             # ValueConverter verification
        └── SettingsTests.cs               # Settings and favorite persistence tests
```

---

## System Requirements

* **Operating System:** Windows 10 Version 2004 (Build 19041) or Windows 11
* **Hardware:** Bluetooth 4.0+ adapter with A2DP Sink support
* **Runtime:** [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) (or run the self-contained build)

---

## Getting Started

### 1. Prerequisites
Ensure you have the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed:
```powershell
dotnet --version
```

### 2. Build the Project
```powershell
dotnet build -c Release
```

### 3. Run the Application
```powershell
dotnet run --project src/MoonbeamAudioReceiver/MoonbeamAudioReceiver.csproj
```

### 4. Run Automated Tests
```powershell
dotnet test
```

---

## Publishing as a Standalone Executable

To package Moonbeam Audio Receiver as a single, portable `.exe` that runs on any Windows 10/11 machine without requiring users to install .NET 10:

```powershell
dotnet publish src/MoonbeamAudioReceiver/MoonbeamAudioReceiver.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

The compiled binary will be generated at:
```
src/MoonbeamAudioReceiver/bin/Release/net10.0-windows10.0.22621.0/win-x64/publish/MoonbeamAudioReceiver.exe
```

---

## Usage Guide

1. **Pairing a Device:**
   * Turn on Bluetooth on your phone/tablet and make it discoverable.
   * If the device appears under **Nearby & Paired Bluetooth Devices**, click **Pair**.
   * Alternatively, click **Windows Settings** to pair via Windows standard Bluetooth settings.
2. **Connecting & Streaming:**
   * Click **Connect** next to any paired device.
   * Play music or podcasts on your phone; audio will render through your PC's default audio output.
3. **Setting a Favorite:**
   * Click the **Star (★)** icon next to any paired device.
   * If **Auto-connect preferred device** is checked, Moonbeam will automatically connect when the phone comes in range.
4. **Background / System Tray:**
   * Enable **Minimize to tray on close** to keep the receiver running in the system tray when closing the window.
   * Double-click the tray icon to restore the window.

---

## Configuration

Application preferences are automatically saved in JSON format at:
```
%APPDATA%\MoonbeamAudioReceiver\settings.json
```

```json
{
  "AutoConnectPreferredDevice": true,
  "PreferredDeviceId": "BluetoothDevice#...",
  "LastConnectedDeviceId": "BluetoothDevice#...",
  "MinimizeToTrayOnClose": true
}
```

---

## Troubleshooting

* **Device not appearing in the list:**
  * Click the **Refresh** button in the header.
  * Ensure Bluetooth is turned on in Windows (`ms-settings:bluetooth`).
* **Connection fails on click:**
  * Ensure the phone is not already actively streaming to another Bluetooth speaker or car stereo.
  * Disconnect and reconnect Bluetooth on the transmitting device.

---

## License

MIT License. See [LICENSE](LICENSE) for details.
