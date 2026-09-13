using System.Windows;
using MoonbeamAudioReceiver.Services;
using MoonbeamAudioReceiver.ViewModels;

namespace MoonbeamAudioReceiver;

public partial class App : Application
{
    public static ISettingsService SettingsService { get; } = new SettingsService();
    public static IBluetoothReceiverService BluetoothService { get; } = new BluetoothReceiverService(SettingsService);
    public static MainViewModel MainViewModel { get; } = new MainViewModel(BluetoothService, SettingsService);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SettingsService.Load();
        MainViewModel.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        BluetoothService.Dispose();
        SettingsService.Save();
        base.OnExit(e);
    }
}
