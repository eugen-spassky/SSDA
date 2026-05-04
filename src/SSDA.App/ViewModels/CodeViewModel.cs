using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Crypto;
using SSDA.Core.Services;

namespace SSDA.App.ViewModels;

/// <summary>
/// Drives the big TOTP code display — recomputes the current Steam Guard code and
/// remaining-window fraction every 250&#160;ms while the page is visible.
/// </summary>
public sealed partial class CodeViewModel : ObservableObject, IDisposable
{
    private readonly ISystemClock _clock;
    private readonly DispatcherTimer _timer;
    private string? _sharedSecret;

    [ObservableProperty]
    private string _code = "—";

    [ObservableProperty]
    private int _secondsRemaining = 30;

    [ObservableProperty]
    private double _progress = 1.0;

    [ObservableProperty]
    private bool _hasAccount;

    public CodeViewModel(ISystemClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Switches the active account and immediately refreshes the code.</summary>
    public void SetAccount(AccountViewModel? account)
    {
        _sharedSecret = account?.Account.SharedSecret;
        HasAccount = !string.IsNullOrEmpty(_sharedSecret);
        Tick();
    }

    /// <summary>Begins the dispatcher tick.</summary>
    public void Start() => _timer.Start();

    /// <summary>Stops the dispatcher tick. Idempotent.</summary>
    public void Stop() => _timer.Stop();

    [RelayCommand]
    private void Copy()
    {
        if (!HasAccount || string.IsNullOrEmpty(Code) || Code == "—") return;
        try
        {
            System.Windows.Clipboard.SetText(Code);
        }
        catch
        {
            // ignored — clipboard may be locked by another app
        }
    }

    private void Tick()
    {
        var now = _clock.UtcNowUnixSeconds();
        SecondsRemaining = SteamTotpGenerator.SecondsRemainingInWindow(now);
        Progress = SecondsRemaining / (double)SteamTotpGenerator.StepSeconds;
        Code = HasAccount
            ? SteamTotpGenerator.Generate(_sharedSecret!, now)
            : "—";
    }

    /// <inheritdoc/>
    public void Dispose() => _timer.Stop();
}
