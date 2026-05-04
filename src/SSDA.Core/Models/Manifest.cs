using System.Text.Json.Serialization;

namespace SSDA.Core.Models;

/// <summary>
/// Top-level <c>maFiles/manifest.json</c> describing every account file stored on disk and
/// the global SDA preferences. The on-disk schema is fixed by the original SDA implementation.
/// </summary>
public sealed class Manifest
{
    [JsonPropertyName("encrypted")]
    public bool Encrypted { get; set; }

    [JsonPropertyName("first_run")]
    public bool FirstRun { get; set; } = true;

    [JsonPropertyName("entries")]
    public List<ManifestEntry> Entries { get; set; } = new();

    [JsonPropertyName("periodic_checking")]
    public bool PeriodicChecking { get; set; }

    [JsonPropertyName("periodic_checking_interval")]
    public int PeriodicCheckingInterval { get; set; } = 5;

    [JsonPropertyName("periodic_checking_checkall")]
    public bool CheckAllAccounts { get; set; }

    [JsonPropertyName("auto_confirm_market_transactions")]
    public bool AutoConfirmMarketTransactions { get; set; }

    [JsonPropertyName("auto_confirm_trades")]
    public bool AutoConfirmTrades { get; set; }
}

public sealed class ManifestEntry
{
    [JsonPropertyName("encryption_iv")]
    public string? IV { get; set; }

    [JsonPropertyName("encryption_salt")]
    public string? Salt { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("steamid")]
    public ulong SteamID { get; set; }
}
