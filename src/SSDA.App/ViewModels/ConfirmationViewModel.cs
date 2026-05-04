using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

/// <summary>One row on the «All confirmations» page.</summary>
public sealed partial class ConfirmationViewModel : ObservableObject
{
    private readonly Func<ConfirmationViewModel, bool, CancellationToken, Task>? _act;

    public Confirmation Source { get; }
    public AccountViewModel Account { get; }

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _ageText = string.Empty;

    [ObservableProperty]
    private string _accountName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActionable))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActionable))]
    private bool _isResolved;

    [ObservableProperty]
    private string _resolution = string.Empty;

    public bool IsActionable => !IsBusy && !IsResolved;

    public ConfirmationViewModel(
        Confirmation source,
        AccountViewModel account,
        Func<ConfirmationViewModel, bool, CancellationToken, Task>? act = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Account = account ?? throw new ArgumentNullException(nameof(account));
        AccountName = account.DisplayName;
        _act = act;
        KindLabel = source.Type switch
        {
            ConfirmationType.Trade => "TRADE",
            ConfirmationType.MarketListing => "MARKET",
            ConfirmationType.PhoneNumberChange => "PHONE",
            ConfirmationType.AccountRecovery => "RECOVERY",
            ConfirmationType.FeatureOptOut => "OPT-OUT",
            ConfirmationType.AccountAuthentication => "LOGIN",
            ConfirmationType.ApiKeyCreation => "API KEY",
            ConfirmationType.Test => "TEST",
            _ => "OTHER",
        };
        Headline = source.Headline ?? "(no headline)";
        Summary = string.Join(" · ", source.Summary);
        AgeText = FormatAge(source.CreationTime);
    }

    [RelayCommand]
    private Task Accept() => RunAsync(accept: true);

    [RelayCommand]
    private Task Deny() => RunAsync(accept: false);

    private async Task RunAsync(bool accept)
    {
        if (_act is null || IsBusy || IsResolved) return;
        try
        {
            IsBusy = true;
            await _act(this, accept, CancellationToken.None).ConfigureAwait(true);
            IsResolved = true;
            Resolution = accept ? "Принято" : "Отклонено";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatAge(long unixSeconds)
    {
        if (unixSeconds <= 0) return "just now";
        var when = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var delta = DateTimeOffset.UtcNow - when;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} h ago";
        return $"{(int)delta.TotalDays} d ago";
    }
}
