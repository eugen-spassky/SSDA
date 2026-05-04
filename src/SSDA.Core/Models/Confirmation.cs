using System.Text.Json.Serialization;

namespace SSDA.Core.Models;

/// <summary>
/// One pending mobile confirmation returned by <c>steamcommunity.com/mobileconf/getlist</c>.
/// Used for trades, market listings, login approvals, phone-number changes, and
/// account-recovery flows.
/// </summary>
public sealed class Confirmation
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("nonce")]
    public ulong Nonce { get; set; }

    [JsonPropertyName("creator_id")]
    public ulong CreatorId { get; set; }

    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("summary")]
    public List<string> Summary { get; set; } = new();

    [JsonPropertyName("accept")]
    public string? AcceptText { get; set; }

    [JsonPropertyName("cancel")]
    public string? CancelText { get; set; }

    [JsonPropertyName("icon")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("type")]
    public ConfirmationType Type { get; set; }

    [JsonPropertyName("type_name")]
    public string? TypeName { get; set; }

    [JsonPropertyName("creation_time")]
    public long CreationTime { get; set; }
}

public enum ConfirmationType
{
    Invalid = 0,
    Test = 1,
    Trade = 2,
    MarketListing = 3,
    FeatureOptOut = 4,
    PhoneNumberChange = 5,
    AccountRecovery = 6,
    AccountAuthentication = 8,
    ApiKeyCreation = 11,
    Unknown = 999,
}
