using System.Text.Json.Serialization;

namespace SSDA.Core.Models;

/// <summary>
/// Single account entry stored as <c>{steamid}.maFile</c>. Property names match the original
/// SDA on disk so files can be round-tripped between SSDA and the legacy implementation.
/// </summary>
public sealed class SteamGuardAccount
{
    [JsonPropertyName("shared_secret")]
    public string? SharedSecret { get; set; }

    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("revocation_code")]
    public string? RevocationCode { get; set; }

    [JsonPropertyName("uri")]
    public string? URI { get; set; }

    [JsonPropertyName("server_time")]
    public long ServerTime { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("token_gid")]
    public string? TokenGID { get; set; }

    [JsonPropertyName("identity_secret")]
    public string? IdentitySecret { get; set; }

    [JsonPropertyName("secret_1")]
    public string? Secret1 { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceID { get; set; }

    [JsonPropertyName("fully_enrolled")]
    public bool FullyEnrolled { get; set; }

    [JsonPropertyName("Session")]
    public SessionData? Session { get; set; }
}
