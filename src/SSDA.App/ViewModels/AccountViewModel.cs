using CommunityToolkit.Mvvm.ComponentModel;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

/// <summary>One row in the account list / one bound account on the main page.</summary>
public sealed partial class AccountViewModel : ObservableObject
{
    public SteamGuardAccount Account { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _steamIdText = string.Empty;

    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private bool _hasSession;

    public AccountViewModel(SteamGuardAccount account)
    {
        Account = account ?? throw new ArgumentNullException(nameof(account));
        DisplayName = string.IsNullOrEmpty(account.AccountName) ? "(unnamed)" : account.AccountName!;
        SteamIdText = account.Session?.SteamID.ToString() ?? "—";
        Initials = ComputeInitials(DisplayName);
        HasSession = !string.IsNullOrEmpty(account.Session?.AccessToken);
    }

    private static string ComputeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        var clean = name.Trim();
        if (clean.Length == 1) return clean.ToUpperInvariant();
        return string.Concat(clean.AsSpan(0, 1), clean.AsSpan(1, 1)).ToUpperInvariant();
    }
}
