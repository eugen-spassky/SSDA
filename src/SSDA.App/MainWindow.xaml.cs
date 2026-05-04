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
            _vm.LoadManifest();
            _vm.CodeVm.Start();
        };
        Closed += (_, _) => _vm.CodeVm.Dispose();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
