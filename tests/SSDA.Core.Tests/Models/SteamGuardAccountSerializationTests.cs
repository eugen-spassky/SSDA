using System.Text.Json;
using SSDA.Core.Models;

namespace SSDA.Core.Tests.Models;

public sealed class SteamGuardAccountSerializationTests
{
    /// <summary>
    /// Real-world maFile shape (with redacted secrets) produced by the original SDA. We
    /// deserialize it, then re-serialize it, and verify the field names round-trip.
    /// Property casing and snake_case mapping must be preserved or the file would be
    /// silently corrupted on save.
    /// </summary>
    private const string SampleJson = """
    {
      "shared_secret": "AAAAAAAAAAAAAAAAAAAAAAAAAAA=",
      "serial_number": "1234567890",
      "revocation_code": "R12345",
      "uri": "otpauth://totp/Steam:user?secret=...",
      "server_time": 1700000000,
      "account_name": "user",
      "token_gid": "abcdef",
      "identity_secret": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=",
      "secret_1": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC=",
      "status": 1,
      "device_id": "android:00000000-0000-0000-0000-000000000000",
      "fully_enrolled": true,
      "Session": {
        "SteamID": 76561198000000000,
        "AccessToken": "jwt-access",
        "RefreshToken": "jwt-refresh",
        "SessionID": "ABCDEF1234"
      }
    }
    """;

    [Fact]
    public void Deserialize_reads_every_documented_field()
    {
        var account = JsonSerializer.Deserialize<SteamGuardAccount>(SampleJson)!;

        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAA=", account.SharedSecret);
        Assert.Equal("1234567890", account.SerialNumber);
        Assert.Equal("R12345", account.RevocationCode);
        Assert.Equal("user", account.AccountName);
        Assert.Equal(1, account.Status);
        Assert.True(account.FullyEnrolled);

        Assert.NotNull(account.Session);
        Assert.Equal(76561198000000000UL, account.Session!.SteamID);
        Assert.Equal("jwt-access", account.Session.AccessToken);
        Assert.Equal("jwt-refresh", account.Session.RefreshToken);
    }

    [Fact]
    public void Roundtrip_preserves_snake_case_property_names()
    {
        var account = JsonSerializer.Deserialize<SteamGuardAccount>(SampleJson)!;
        var serialized = JsonSerializer.Serialize(account);

        // Property names are the contract — the original SDA reads them via Newtonsoft
        // with explicit JsonProperty attributes so the casing must be exactly this.
        Assert.Contains("\"shared_secret\"", serialized);
        Assert.Contains("\"identity_secret\"", serialized);
        Assert.Contains("\"revocation_code\"", serialized);
        Assert.Contains("\"device_id\"", serialized);
        Assert.Contains("\"fully_enrolled\"", serialized);
        Assert.Contains("\"Session\"", serialized);
        Assert.Contains("\"SteamID\"", serialized);
        Assert.Contains("\"AccessToken\"", serialized);
        Assert.Contains("\"RefreshToken\"", serialized);
    }
}
