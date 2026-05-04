using System.IO;
using Microsoft.Win32;

namespace SSDA.App.Interop;

/// <summary>
/// Toggles the per-user "run on Windows startup" registration via the standard
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> key. Per-user only — no
/// administrator elevation required.
/// </summary>
public static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SSDA";

    /// <summary>True if the autostart registration is currently enabled and points at
    /// the running executable. Stale registrations from a previous install path return
    /// <c>false</c> so the UI can re-enable to update the path.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var current = key?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(current)) return false;
            // Compare against the current executable so that re-installs don't leave
            // the user with a "checked" toggle pointing at a deleted file.
            return string.Equals(NormalizeQuoted(current), GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Registers the running executable to launch on user logon.</summary>
    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open HKCU Run key for writing.");
        key.SetValue(ValueName, $"\"{GetExecutablePath()}\"", RegistryValueKind.String);
    }

    /// <summary>Removes the registration. No-op if absent.</summary>
    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Convenience wrapper that maps a bool onto enable/disable.</summary>
    public static void Set(bool enabled)
    {
        if (enabled) Enable(); else Disable();
    }

    private static string GetExecutablePath()
    {
        // Environment.ProcessPath is the recommended way to get the host EXE on .NET 6+.
        return Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SSDA.exe");
    }

    private static string NormalizeQuoted(string value)
        => value.Trim().Trim('"');
}
