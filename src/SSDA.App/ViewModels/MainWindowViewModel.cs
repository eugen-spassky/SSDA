using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Models;
using SSDA.Core.Services;
using SSDA.Core.Web;

namespace SSDA.App.ViewModels;

/// <summary>
/// Top-level view-model. Owns the loaded accounts, the active page, and coordinates the
/// child code/confirmations view-models. Uses <see cref="ManifestStore"/> for persistence.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly ManifestStore _store;
    private readonly SteamWebContextRegistry _webRegistry;
    private readonly bool _ownsRegistry;
    private readonly object _refreshGate = new();
    private CancellationTokenSource? _refreshCts;
    private Manifest _manifest = new();
    private string? _passkey;

    public ObservableCollection<AccountViewModel> Accounts { get; } = new();

    public CodeViewModel CodeVm { get; }
    public ConfirmationsViewModel ConfirmationsVm { get; }
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

    public MainWindowViewModel(
        ManifestStore store,
        ISystemClock? clock = null,
        SteamWebContextRegistry? webRegistry = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ownsRegistry = webRegistry is null;
        _webRegistry = webRegistry ?? new SteamWebContextRegistry();
        CodeVm = new CodeViewModel(clock);
        ConfirmationsVm = new ConfirmationsViewModel(RefreshConfirmationsAsync);
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
    private async Task NavigateToConfirmations()
    {
        ActivePage = MainPage.Confirmations;
        await RefreshConfirmationsAsync();
    }

    partial void OnSelectedAccountChanged(AccountViewModel? value) => CodeVm.SetAccount(value);

    /// <summary>
    /// Pulls confirmations for every loaded account that has a usable session, merges
    /// them into a single feed, and updates <see cref="ConfirmationsVm"/>.
    /// </summary>
    public async Task RefreshConfirmationsAsync(CancellationToken ct = default)
    {
        if (Accounts.Count == 0) return;

        CancellationTokenSource linked;
        lock (_refreshGate)
        {
            try { _refreshCts?.Cancel(); } catch (ObjectDisposedException) { }
            _refreshCts?.Dispose();
            linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _refreshCts = linked;
        }
        var token = linked.Token;

        ConfirmationsVm.SetLoading(true);
        var aggregate = new List<ConfirmationViewModel>();
        var errors = new List<string>();
        try
        {
            foreach (var account in Accounts)
            {
                token.ThrowIfCancellationRequested();
                if (!account.HasSession) continue;
                try
                {
                    var client = _webRegistry.GetMobileConfClient(account.Account);
                    var list = await client.ListAsync(account.Account, token).ConfigureAwait(true);
                    foreach (var c in list)
                        aggregate.Add(new ConfirmationViewModel(c, account.DisplayName));
                }
                catch (SteamWebUnauthorizedException)
                {
                    errors.Add($"{account.DisplayName}: сессия истекла");
                }
                catch (HttpRequestException ex)
                {
                    errors.Add($"{account.DisplayName}: {ex.Message}");
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested)
                {
                    errors.Add($"{account.DisplayName}: timeout");
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add($"{account.DisplayName}: {ex.Message}");
                }
            }
            ConfirmationsVm.Replace(aggregate);
            ConfirmationsVm.LastError = string.Join(" · ", errors);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Superseded by a newer refresh; let it own the UI state.
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ConfirmationsVm.LastError = ex.Message;
        }
        finally
        {
            lock (_refreshGate)
            {
                if (_refreshCts == linked)
                {
                    ConfirmationsVm.SetLoading(false);
                    _refreshCts = null;
                    linked.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_refreshGate)
        {
            try { _refreshCts?.Cancel(); } catch (ObjectDisposedException) { }
            _refreshCts?.Dispose();
            _refreshCts = null;
        }
        if (_ownsRegistry) _webRegistry.Dispose();
    }

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

        ConfirmationsVm.Replace(Array.Empty<ConfirmationViewModel>());

        if (Accounts.Any(a => a.HasSession))
            _ = SafeRefreshConfirmationsAsync();
    }

    private async Task SafeRefreshConfirmationsAsync()
    {
        try
        {
            await RefreshConfirmationsAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ConfirmationsVm.LastError = ex.Message;
            ConfirmationsVm.SetLoading(false);
        }
    }
}

public enum MainPage
{
    Accounts,
    Confirmations,
}
