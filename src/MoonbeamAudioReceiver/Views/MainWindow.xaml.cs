using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using MoonbeamAudioReceiver.ViewModels;
using Wpf.Ui.Controls;

namespace MoonbeamAudioReceiver.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private Storyboard? _waveformAnimation;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = App.MainViewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _waveformAnimation = FindResource("WaveformAnimation") as Storyboard;
        UpdateWaveformState();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsStreaming))
        {
            UpdateWaveformState();
        }
    }

    private void UpdateWaveformState()
    {
        if (_waveformAnimation == null) return;

        if (_viewModel.IsStreaming)
        {
            _waveformAnimation.Begin(this, isControllable: true);
        }
        else
        {
            _waveformAnimation.Stop(this);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void TrayMenu_Open_Click(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitExit = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void RestoreWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }
}
