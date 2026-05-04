using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.App.ViewModels;

/// <summary>
/// Top-level view-model. Owns the loaded accounts, the active page, and coordinates the
/// child code/confirmations view-models. Uses <see cref="ManifestStore"/> for persistence.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ManifestStore _store;
    private Manifest _manifest = new();
    private string? _passkey;

    public ObservableCollection<AccountViewModel> Accounts { get; } = new();

    public CodeViewModel CodeVm { get; }
    public ConfirmationsViewModel ConfirmationsVm { get; } = new();
    public PassphraseViewModel PassphraseVm { get; }

    [ObservableProperty]
    private AccountViewModel? _selectedAccount;

    [ObservableProperty]
    private MainPage _activePage = MainPage.Accounts;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _hasManifest;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public MainWindowViewModel(ManifestStore store, ISystemClock? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        CodeVm = new CodeViewModel(clock);
        PassphraseVm = new PassphraseViewModel(TryUnlock);
        HasManifest = _store.ManifestExists();
    }

    /// <summary>Loads the manifest in plaintext; sets <see cref="IsLocked"/> if encrypted.</summary>
    public void LoadManifest()
    {
        if (!_store.ManifestExists())
        {
            HasManifest = false;
            return;
        }

        _manifest = _store.LoadManifest();
        HasManifest = true;
        if (_manifest.Encrypted)
        {
            IsLocked = true;
            StatusText = "База зашифрована. Введите passkey для разблокировки.";
            return;
        }

        ApplyAccounts(_store.LoadAccounts(_manifest));
    }

    /// <summary>
    /// Tries to decrypt the manifest with the supplied passkey. Returns <c>true</c> on success.
    /// </summary>
    public bool TryUnlock(string passkey)
    {
        try
        {
            var loaded = _store.LoadAccounts(_manifest, passkey);
            _passkey = passkey;
            IsLocked = false;
            StatusText = string.Empty;
            ApplyAccounts(loaded);
            return true;
        }
        catch (InvalidPasskeyException)
        {
            return false;
        }
    }

    /// <summary>Re-locks the manifest, clearing the in-memory passkey + accounts.</summary>
    [RelayCommand]
    public void Lock()
    {
        _passkey = null;
        Accounts.Clear();
        SelectedAccount = null;
        CodeVm.SetAccount(null);
        ConfirmationsVm.Replace(Array.Empty<ConfirmationViewModel>());
        if (_manifest.Encrypted)
        {
            IsLocked = true;
            StatusText = "Заблокировано.";
        }
    }

    [RelayCommand]
    private void NavigateToAccounts() => ActivePage = MainPage.Accounts;

    [RelayCommand]
    private void NavigateToConfirmations() => ActivePage = MainPage.Confirmations;

    partial void OnSelectedAccountChanged(AccountViewModel? value) => CodeVm.SetAccount(value);

    private void ApplyAccounts(
        IReadOnlyList<(SteamGuardAccount Account, ManifestEntry Entry)> loaded)
    {
        Accounts.Clear();
        foreach (var (account, _) in loaded)
            Accounts.Add(new AccountViewModel(account));

        SelectedAccount = Accounts.FirstOrDefault();
        StatusText = Accounts.Count == 0
            ? "Нет аккаунтов. Привязка будет добавлена в следующем PR."
            : $"Загружено: {Accounts.Count}";

        // Confirmations feed is empty until the Steam Web client lands in the next PR.
        ConfirmationsVm.Replace(Array.Empty<ConfirmationViewModel>());
    }
}

public enum MainPage
{
    Accounts,
    Confirmations,
}
