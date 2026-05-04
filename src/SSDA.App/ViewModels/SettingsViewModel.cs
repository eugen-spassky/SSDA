using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SSDA.App.Interop;

namespace SSDA.App.ViewModels;

/// <summary>
/// Backs the Settings page: legacy-SDA import + autostart + minimize-to-tray toggles.
/// All state aside from autostart lives only in memory + on the parent
/// <see cref="MainWindowViewModel"/>; tray/import settings are session-scoped to keep
/// the surface area small.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Func<string, string?, string?, ImportRequestResult>? _importAsync;

    [ObservableProperty] private bool _autostartEnabled;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public SettingsViewModel() : this(null) { }

    public SettingsViewModel(Func<string, string?, string?, ImportRequestResult>? importAsync)
    {
        _importAsync = importAsync;
        try { _autostartEnabled = AutostartManager.IsEnabled; }
        catch { _autostartEnabled = false; /* registry may be locked down */ }
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        try
        {
            AutostartManager.Set(value);
            StatusText = value ? "Автозапуск включён." : "Автозапуск отключён.";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось изменить автозапуск: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Import()
    {
        if (_importAsync is null || IsBusy) return;

        // Default Windows path for the legacy Steam Desktop Authenticator.
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Steam Desktop Authenticator",
            "maFiles");

        var dialog = new OpenFileDialog
        {
            Title = "Выберите manifest.json от Steam Desktop Authenticator",
            Filter = "manifest.json|manifest.json|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(legacy) ? legacy : null,
        };
        if (dialog.ShowDialog() != true) return;

        var sourceDir = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(sourceDir))
        {
            StatusText = "Не удалось определить папку с maFiles.";
            return;
        }

        StatusText = "Импорт…";
        IsBusy = true;
        try
        {
            // The host owns passkey state, so it decides whether to prompt the user
            // for the source / destination passkeys before delegating to the importer.
            var result = _importAsync(sourceDir, null, null);
            StatusText = result.StatusMessage;
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка импорта: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>UI-friendly summary returned by the host's import callback.</summary>
public sealed class ImportRequestResult
{
    public required string StatusMessage { get; init; }
}
