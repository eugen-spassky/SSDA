using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

/// <summary>
/// Backing model for the «All confirmations» page. Holds the merged feed of mobile
/// confirmations across every loaded account and lets the user filter by type.
/// </summary>
public sealed partial class ConfirmationsViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task>? _refresh;

    public ObservableCollection<ConfirmationViewModel> All { get; } = new();
    public ObservableCollection<ConfirmationViewModel> Visible { get; } = new();

    [ObservableProperty]
    private ConfirmationFilter _activeFilter = ConfirmationFilter.All;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _lastError = string.Empty;

    public ConfirmationsViewModel() : this(null) { }

    public ConfirmationsViewModel(Func<CancellationToken, Task>? refresh)
    {
        _refresh = refresh;
        All.CollectionChanged += (_, _) => RebuildVisible();
        RebuildVisible();
    }

    /// <summary>Replaces the entire backing list. Used when accounts reload.</summary>
    public void Replace(IEnumerable<ConfirmationViewModel> confirmations)
    {
        All.Clear();
        foreach (var c in confirmations) All.Add(c);
    }

    public void SetLoading(bool value) => IsLoading = value;

    [RelayCommand]
    private void SetFilter(string? filter)
    {
        if (Enum.TryParse<ConfirmationFilter>(filter, ignoreCase: true, out var parsed))
            ActiveFilter = parsed;
    }

    [RelayCommand]
    private Task Refresh() => _refresh?.Invoke(CancellationToken.None) ?? Task.CompletedTask;

    partial void OnActiveFilterChanged(ConfirmationFilter value) => RebuildVisible();

    private void RebuildVisible()
    {
        Visible.Clear();
        foreach (var c in All)
        {
            if (Matches(c.Source.Type, ActiveFilter))
                Visible.Add(c);
        }
        IsEmpty = Visible.Count == 0;
    }

    private static bool Matches(ConfirmationType type, ConfirmationFilter filter) => filter switch
    {
        ConfirmationFilter.All => true,
        ConfirmationFilter.Trades => type == ConfirmationType.Trade,
        ConfirmationFilter.Market => type == ConfirmationType.MarketListing,
        ConfirmationFilter.Login => type == ConfirmationType.AccountAuthentication,
        ConfirmationFilter.Other =>
            type != ConfirmationType.Trade
            && type != ConfirmationType.MarketListing
            && type != ConfirmationType.AccountAuthentication,
        _ => true,
    };
}

public enum ConfirmationFilter
{
    All,
    Trades,
    Market,
    Login,
    Other,
}
