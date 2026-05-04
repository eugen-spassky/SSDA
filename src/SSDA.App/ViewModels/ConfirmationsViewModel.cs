using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSDA.Core.Models;

namespace SSDA.App.ViewModels;

/// <summary>
/// Backing model for the «All confirmations» page. Holds the merged feed of mobile
/// confirmations across every loaded account and lets the user filter by type.
/// The actual fetch + accept/deny flow ships in the next PR — this PR wires up the
/// page so the UX is reachable and the rendering is finalised.
/// </summary>
public sealed partial class ConfirmationsViewModel : ObservableObject
{
    public ObservableCollection<ConfirmationViewModel> All { get; } = new();
    public ObservableCollection<ConfirmationViewModel> Visible { get; } = new();

    [ObservableProperty]
    private ConfirmationFilter _activeFilter = ConfirmationFilter.All;

    [ObservableProperty]
    private bool _isEmpty = true;

    public ConfirmationsViewModel()
    {
        All.CollectionChanged += (_, _) => RebuildVisible();
        RebuildVisible();
    }

    /// <summary>Replaces the entire backing list. Used when accounts reload.</summary>
    public void Replace(IEnumerable<ConfirmationViewModel> confirmations)
    {
        All.Clear();
        foreach (var c in confirmations) All.Add(c);
    }

    [RelayCommand]
    private void SetFilter(ConfirmationFilter filter)
    {
        ActiveFilter = filter;
        RebuildVisible();
    }

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
        ConfirmationFilter.Login => type == ConfirmationType.AccountRecovery,
        ConfirmationFilter.Other =>
            type != ConfirmationType.Trade
            && type != ConfirmationType.MarketListing
            && type != ConfirmationType.AccountRecovery,
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
