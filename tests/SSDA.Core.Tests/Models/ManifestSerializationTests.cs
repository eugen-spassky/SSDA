using System.Text.Json;
using SSDA.Core.Models;

namespace SSDA.Core.Tests.Models;

public sealed class ManifestSerializationTests
{
    private const string SampleJson = """
    {
      "encrypted": true,
      "first_run": false,
      "entries": [
        {
          "encryption_iv": "AAAAAAAAAAAAAAAAAAAAAA==",
          "encryption_salt": "BBBBBBBBBBA=",
          "filename": "76561198000000000.maFile",
          "steamid": 76561198000000000
        }
      ],
      "periodic_checking": true,
      "periodic_checking_interval": 7,
      "periodic_checking_checkall": false,
      "auto_confirm_market_transactions": true,
      "auto_confirm_trades": false
    }
    """;

    [Fact]
    public void Deserialize_reads_every_documented_field()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(SampleJson)!;

        Assert.True(manifest.Encrypted);
        Assert.False(manifest.FirstRun);
        Assert.True(manifest.PeriodicChecking);
        Assert.Equal(7, manifest.PeriodicCheckingInterval);
        Assert.False(manifest.CheckAllAccounts);
        Assert.True(manifest.AutoConfirmMarketTransactions);
        Assert.False(manifest.AutoConfirmTrades);

        var entry = Assert.Single(manifest.Entries);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA==", entry.IV);
        Assert.Equal("BBBBBBBBBBA=", entry.Salt);
        Assert.Equal("76561198000000000.maFile", entry.Filename);
        Assert.Equal(76561198000000000UL, entry.SteamID);
    }

    [Fact]
    public void Roundtrip_preserves_snake_case_property_names()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(SampleJson)!;
        var serialized = JsonSerializer.Serialize(manifest);

        Assert.Contains("\"encrypted\"", serialized);
        Assert.Contains("\"first_run\"", serialized);
        Assert.Contains("\"entries\"", serialized);
        Assert.Contains("\"encryption_iv\"", serialized);
        Assert.Contains("\"encryption_salt\"", serialized);
        Assert.Contains("\"filename\"", serialized);
        Assert.Contains("\"steamid\"", serialized);
        Assert.Contains("\"periodic_checking\"", serialized);
        Assert.Contains("\"periodic_checking_interval\"", serialized);
        Assert.Contains("\"periodic_checking_checkall\"", serialized);
        Assert.Contains("\"auto_confirm_market_transactions\"", serialized);
        Assert.Contains("\"auto_confirm_trades\"", serialized);
    }

    [Fact]
    public void Default_manifest_has_first_run_true_and_periodic_interval_five()
    {
        var manifest = new Manifest();
        Assert.True(manifest.FirstRun);
        Assert.Equal(5, manifest.PeriodicCheckingInterval);
        Assert.False(manifest.Encrypted);
        Assert.Empty(manifest.Entries);
    }
}
