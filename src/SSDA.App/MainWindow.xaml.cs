using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SSDA.App.Interop;
using SSDA.App.ViewModels;
using SSDA.Core.Services;

namespace SSDA.App;

/// <summary>The shell window. Owns the <see cref="MainWindowViewModel"/> and applies Mica.</summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private TrayIconHost? _tray;

    public MainWindow()
    {
        InitializeComponent();

        var maFilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SSDA",
            "maFiles");
        Directory.CreateDirectory(maFilesDir);

        var store = new ManifestStore(maFilesDir);
        _vm = new MainWindowViewModel(store);
        DataContext = _vm;
        SourceInitialized += (_, _) => DwmBackdrop.Apply(this, DwmBackdrop.BackdropKind.Mica);
        Loaded += (_, _) =>
        {
            _tray = new TrayIconHost(this);
            _vm.LoadManifest();
            _vm.CodeVm.Start();
        };
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _vm.CodeVm.Dispose();
            _vm.Dispose();
            _tray?.Dispose();
        };
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // When the user clicks the taskbar minimise button, hide the window into the
        // tray (the icon stays visible) so the OS doesn't keep an empty taskbar entry
        // around. Restoring is via tray double-click or the context-menu "Открыть".
        if (WindowState == WindowState.Minimized && _vm.SettingsVm.MinimizeToTray)
        {
            _tray?.HideToTray();
            // Reset the state to Normal in advance so the next Show() comes back properly.
            WindowState = WindowState.Normal;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Closing via "✕" hides into the tray. Real exit goes through the tray's
        // Exit menu, which sets `IsClosing = true` and re-issues Close + Shutdown.
        if (_tray is null || _tray.IsClosing) return;
        if (!_vm.SettingsVm.MinimizeToTray) return;
        e.Cancel = true;
        _tray.HideToTray();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
