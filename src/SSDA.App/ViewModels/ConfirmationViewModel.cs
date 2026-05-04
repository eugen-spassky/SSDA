using CommunityToolkit.Mvvm.ComponentModel;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

/// <summary>One row on the «All confirmations» page.</summary>
public sealed partial class ConfirmationViewModel : ObservableObject
{
    public Confirmation Source { get; }

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

    public ConfirmationViewModel(Confirmation source, string accountName)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        AccountName = accountName;
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
