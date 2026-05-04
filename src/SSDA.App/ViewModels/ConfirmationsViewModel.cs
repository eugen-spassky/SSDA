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
    private readonly Func<IReadOnlyList<ConfirmationViewModel>, bool, CancellationToken, Task>? _bulkRespond;

    public ObservableCollection<ConfirmationViewModel> All { get; } = new();
    public ObservableCollection<ConfirmationViewModel> Visible { get; } = new();

    [ObservableProperty]
    private ConfirmationFilter _activeFilter = ConfirmationFilter.All;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBulkBusy;

    [ObservableProperty]
    private string _lastError = string.Empty;

    public ConfirmationsViewModel() : this(null, null) { }

    public ConfirmationsViewModel(
        Func<CancellationToken, Task>? refresh,
        Func<IReadOnlyList<ConfirmationViewModel>, bool, CancellationToken, Task>? bulkRespond = null)
    {
        _refresh = refresh;
        _bulkRespond = bulkRespond;
        All.CollectionChanged += (_, _) => RebuildVisible();
        RebuildVisible();
    }

    /// <summary>Replaces the entire backing list. Used when accounts reload.</summary>
    public void Replace(IEnumerable<ConfirmationViewModel> confirmations)
    {
        All.Clear();
        foreach (var c in confirmations) All.Add(c);
    }

    /// <summary>Removes a single confirmation (after it has been resolved against Steam).</summary>
    public void Remove(ConfirmationViewModel confirmation) => All.Remove(confirmation);

    public void SetLoading(bool value) => IsLoading = value;

    [RelayCommand]
    private void SetFilter(string? filter)
    {
        if (Enum.TryParse<ConfirmationFilter>(filter, ignoreCase: true, out var parsed))
            ActiveFilter = parsed;
    }

    [RelayCommand]
    private Task Refresh() => _refresh?.Invoke(CancellationToken.None) ?? Task.CompletedTask;

    [RelayCommand]
    private Task AcceptAllVisible() => BulkAsync(accept: true);

    [RelayCommand]
    private Task DenyAllVisible() => BulkAsync(accept: false);

    private async Task BulkAsync(bool accept)
    {
        if (_bulkRespond is null || IsBulkBusy) return;
        var snapshot = Visible.Where(v => v.IsActionable).ToList();
        if (snapshot.Count == 0) return;

        try
        {
            IsBulkBusy = true;
            await _bulkRespond(snapshot, accept, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBulkBusy = false;
        }
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
