using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SSDA.App.ViewModels;

/// <summary>
/// Bound to the re-login modal. Owns no Steam credentials directly; the host wires the
/// <see cref="LoginAsync"/> callback that talks to <c>SSDA.Core.Auth.SteamLoginClient</c>
/// and updates the affected <see cref="AccountViewModel"/> on success.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly Func<AccountViewModel, string, CancellationToken, Task>? _loginAsync;

    [ObservableProperty]
    private AccountViewModel? _target;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public LoginViewModel() : this(null) { }

    public LoginViewModel(Func<AccountViewModel, string, CancellationToken, Task>? loginAsync)
    {
        _loginAsync = loginAsync;
    }

    /// <summary>Opens the modal for the supplied account.</summary>
    public void Open(AccountViewModel account)
    {
        Target = account ?? throw new ArgumentNullException(nameof(account));
        ErrorMessage = string.Empty;
        IsOpen = true;
    }

    /// <summary>Closes the modal without attempting login.</summary>
    [RelayCommand]
    public void Cancel()
    {
        IsOpen = false;
        Target = null;
        ErrorMessage = string.Empty;
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Submit(string? password)
    {
        if (Target is null || _loginAsync is null) return;
        if (string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Введите пароль.";
            return;
        }
        if (IsBusy) return;

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _loginAsync(Target, password, CancellationToken.None).ConfigureAwait(true);
            IsOpen = false;
            Target = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Convenience for views to invoke the submit command from a key-down handler.</summary>
    public ICommand AsCommand => SubmitCommand;
}
