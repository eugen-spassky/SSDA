using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace SSDA.App.Interop;

/// <summary>
/// Owns a system-tray icon that lets the user minimise the main window into the tray
/// and bring it back. Lives for the lifetime of the application; <see cref="Dispose"/>
/// releases the underlying handle so the icon disappears immediately on shutdown.
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private readonly Window _window;
    private readonly TaskbarIcon _icon;
    private bool _isClosing;

    public TrayIconHost(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        var open = new MenuItem { Header = "Открыть SSDA" };
        open.Click += (_, _) => RestoreWindow();

        var exit = new MenuItem { Header = "Выход" };
        exit.Click += (_, _) =>
        {
            // Mark the close as a real exit so the window's Closing handler doesn't
            // re-hide it into the tray. ShutdownMode="OnMainWindowClose" handles the
            // rest — explicitly calling Application.Current.Shutdown() here would
            // double-fire the shutdown sequence.
            _isClosing = true;
            _window.Close();
        };

        var menu = new ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon = new TaskbarIcon
        {
            ToolTipText = "SSDA",
            ContextMenu = menu,
            IconSource = LoadIconFromExecutable(),
            Visibility = Visibility.Visible,
        };
        _icon.TrayMouseDoubleClick += (_, _) => RestoreWindow();
    }

    /// <summary>Pulls the icon out of the running EXE so the tray indicator matches
    /// the taskbar icon. Returns <c>null</c> if extraction fails (e.g. unit-test host).</summary>
    private static System.Windows.Media.ImageSource? LoadIconFromExecutable()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;
            using var icon = Icon.ExtractAssociatedIcon(exe);
            if (icon is null) return null;
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Brings the main window back from the tray and focuses it.</summary>
    public void RestoreWindow()
    {
        if (_isClosing) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    /// <summary>Hides the main window into the tray (no taskbar entry).</summary>
    public void HideToTray()
    {
        if (_isClosing) return;
        _window.Hide();
    }

    /// <summary>True while the application is on its way out — used by callers
    /// that need to distinguish a real close from a hide-to-tray.</summary>
    public bool IsClosing => _isClosing;

    public void Dispose() => _icon.Dispose();
}
