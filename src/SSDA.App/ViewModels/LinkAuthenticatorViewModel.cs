using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Linking;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

public enum LinkStep
{
    Credentials,
    Sms,
    Revocation,
    Done,
}

/// <summary>
/// Drives the four-step modal that links a brand-new Steam mobile authenticator to the
/// app: enter credentials → Steam SMS → save the revocation code → done.
/// </summary>
public sealed partial class LinkAuthenticatorViewModel : ObservableObject
{
    private readonly Func<string, string, CancellationToken, Task<LinkAuthenticatorContext>>? _signIn;
    private readonly Func<LinkAuthenticatorContext, string, CancellationToken, Task<FinalizeResult>>? _finalize;
    private readonly Action<LinkAuthenticatorContext>? _persist;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private LinkStep _step;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _username = string.Empty;

    /// <summary>The user-visible revocation code on the final screen (e.g. <c>R12345</c>).</summary>
    [ObservableProperty] private string _revocationCode = string.Empty;

    /// <summary>The newly-issued account, populated after step 1.</summary>
    private LinkAuthenticatorContext? _ctx;

    public LinkAuthenticatorViewModel() : this(null, null, null) { }

    public LinkAuthenticatorViewModel(
        Func<string, string, CancellationToken, Task<LinkAuthenticatorContext>>? signIn,
        Func<LinkAuthenticatorContext, string, CancellationToken, Task<FinalizeResult>>? finalize,
        Action<LinkAuthenticatorContext>? persist)
    {
        _signIn = signIn;
        _finalize = finalize;
        _persist = persist;
    }

    public void Open()
    {
        Step = LinkStep.Credentials;
        IsOpen = true;
        IsBusy = false;
        ErrorMessage = string.Empty;
        Username = string.Empty;
        RevocationCode = string.Empty;
        _ctx = null;
    }

    [RelayCommand]
    public void Cancel()
    {
        IsOpen = false;
        IsBusy = false;
        ErrorMessage = string.Empty;
        _ctx = null;
    }

    /// <summary>Step 1 → Step 2: sign in + AddAuthenticator. Password is supplied by the view.</summary>
    [RelayCommand]
    private async Task Credentials(string? password)
    {
        if (_signIn is null) return;
        if (string.IsNullOrEmpty(Username))
        {
            ErrorMessage = "Введите имя пользователя.";
            return;
        }
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
            _ctx = await _signIn(Username, password, CancellationToken.None).ConfigureAwait(true);
            Step = LinkStep.Sms;
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

    /// <summary>Step 2 → Step 3: submit SMS code, finalize, persist maFile.</summary>
    [RelayCommand]
    private async Task Sms(string? smsCode)
    {
        if (_finalize is null || _ctx is null) return;
        if (string.IsNullOrEmpty(smsCode))
        {
            ErrorMessage = "Введите код из SMS.";
            return;
        }
        if (IsBusy) return;

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _finalize(_ctx, smsCode, CancellationToken.None).ConfigureAwait(true);
            switch (result)
            {
                case FinalizeResult.Success:
                    // Surface the revocation code BEFORE persisting. Steam already considers
                    // the authenticator active at this point, so the secrets MUST reach the
                    // user even if writing the maFile to disk fails — otherwise they are
                    // locked out of detaching the authenticator forever.
                    RevocationCode = _ctx.Account.RevocationCode ?? string.Empty;
                    Step = LinkStep.Revocation;
                    try
                    {
                        _persist?.Invoke(_ctx);
                    }
                    catch (Exception persistEx)
                    {
                        ErrorMessage =
                            "Не удалось сохранить maFile на диск: " + persistEx.Message +
                            ". Обязательно сохраните код восстановления вручную!";
                    }
                    break;
                case FinalizeResult.BadSMSCode:
                    ErrorMessage = "Неверный SMS-код, попробуйте ещё раз.";
                    break;
                case FinalizeResult.UnableToGenerateCorrectCodes:
                    ErrorMessage = "Часы устройства расходятся со Steam. Синхронизируйте время.";
                    break;
                default:
                    ErrorMessage = "Steam отклонил привязку. Повторите позже.";
                    break;
            }
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

    /// <summary>Step 3 → Done: user confirmed they saved the revocation code.</summary>
    [RelayCommand]
    private void RevocationAcknowledged()
    {
        Step = LinkStep.Done;
    }

    [RelayCommand]
    private void Finish()
    {
        IsOpen = false;
        _ctx = null;
    }
}

/// <summary>
/// Carrier for state that flows from sign-in (<see cref="SteamId"/>, <see cref="AccessToken"/>)
/// through <c>AddAuthenticatorAsync</c> (<see cref="Account"/>) to <c>FinalizeAsync</c>.
/// </summary>
public sealed class LinkAuthenticatorContext
{
    public required ulong SteamId { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required SteamGuardAccount Account { get; init; }
}
