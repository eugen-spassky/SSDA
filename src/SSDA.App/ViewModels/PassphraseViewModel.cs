using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SSDA.App.ViewModels;

/// <summary>
/// Bound to the passphrase dialog. The hosting view is responsible for invoking
/// <see cref="UnlockCommand"/> with the typed-in passkey (PasswordBox cannot be data-bound
/// directly without a behaviour).
/// </summary>
public sealed partial class PassphraseViewModel : ObservableObject
{
    private readonly Func<string, bool> _tryUnlock;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public PassphraseViewModel(Func<string, bool> tryUnlock)
    {
        _tryUnlock = tryUnlock ?? throw new ArgumentNullException(nameof(tryUnlock));
    }

    [RelayCommand]
    private void Unlock(string? passkey)
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrEmpty(passkey))
        {
            ErrorMessage = "Введите passkey.";
            return;
        }

        IsBusy = true;
        try
        {
            if (!_tryUnlock(passkey))
                ErrorMessage = "Неверный passkey. Попробуйте ещё раз.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Convenience for views to invoke the command from a key-down handler.</summary>
    public ICommand AsCommand => UnlockCommand;
}
