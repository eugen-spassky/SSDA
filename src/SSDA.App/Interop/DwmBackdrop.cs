using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SSDA.App.Interop;

/// <summary>
/// Applies a Windows 11 system backdrop (Mica or Acrylic) to a WPF window via DWM. On
/// Windows 10 and below the calls silently no-op; the application falls back to its
/// solid backdrop brush. Always call <see cref="Apply"/> from the window's
/// <c>SourceInitialized</c> handler so the HWND exists.
/// </summary>
public static class DwmBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaWindowCornerPreference = 33;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    /// <summary>The system backdrop variant to apply.</summary>
    public enum BackdropKind
    {
        /// <summary>Disable the system backdrop and let the window draw its own background.</summary>
        None = 1,
        /// <summary>Mica — best for top-level long-lived windows. Requires Windows 11 22H2+.</summary>
        Mica = 2,
        /// <summary>Acrylic — translucent. Heavier than Mica but works in popups too.</summary>
        Acrylic = 3,
        /// <summary>Mica Alt (a.k.a. tabbed) — variant tinted toward the desktop wallpaper.</summary>
        MicaAlt = 4,
    }

    /// <summary>Window corner radius preference exposed via the DWM API on Windows 11.</summary>
    public enum CornerPreference
    {
        /// <summary>OS default (typically rounded on Win11).</summary>
        Default = 0,
        /// <summary>Square (no rounding).</summary>
        DoNotRound = 1,
        /// <summary>Round with the standard radius (8 px on Win11).</summary>
        Round = 2,
        /// <summary>Round with a smaller radius (4 px on Win11) — for menus/tooltips.</summary>
        RoundSmall = 3,
    }

    /// <summary>
    /// Applies the supplied backdrop to <paramref name="window"/> and switches the system
    /// title-bar / non-client area into dark mode so the (transparent) caption matches the
    /// app theme. The window must be transparent to itself for the backdrop to show through;
    /// callers should set <c>Background = Transparent</c> on the root template.
    /// </summary>
    public static void Apply(Window window, BackdropKind backdrop = BackdropKind.Mica)
    {
        ArgumentNullException.ThrowIfNull(window);

        var helper = new WindowInteropHelper(window);
        var hwnd = helper.EnsureHandle();

        var darkMode = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        var backdropValue = (int)backdrop;
        var hr = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdropValue, sizeof(int));

        // On Windows 10 the call returns E_INVALIDARG; we paint the fallback brush instead.
        if (hr != 0)
        {
            window.Background = (Brush)window.FindResource("WindowBackdropFallbackBrush");
        }
    }

    /// <summary>
    /// Asks the DWM to render <paramref name="window"/> with rounded corners. On
    /// Windows 11 this rounds the actual frame at the compositor level so the result
    /// works with both system and custom title bars; on older Windows versions the
    /// call silently no-ops and corners remain square.
    /// </summary>
    public static void ApplyCornerPreference(Window window, CornerPreference preference = CornerPreference.Round)
    {
        ArgumentNullException.ThrowIfNull(window);

        var helper = new WindowInteropHelper(window);
        var hwnd = helper.EnsureHandle();

        var value = (int)preference;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref value, sizeof(int));
    }
}
