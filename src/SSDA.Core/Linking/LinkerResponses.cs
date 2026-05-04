using System.Text.Json.Serialization;

namespace SSDA.Core.Linking;

/// <summary>
/// Top-level wrapper for Steam's <c>ITwoFactorService/AddAuthenticator/v1</c> response.
/// </summary>
internal sealed class AddAuthenticatorResponse
{
    [JsonPropertyName("response")]
    public AddAuthenticatorPayload? Response { get; set; }
}

/// <summary>
/// Inner payload of <c>AddAuthenticator</c>. Most fields map directly onto
/// <see cref="SSDA.Core.Models.SteamGuardAccount"/> and are copied by
/// <see cref="AuthenticatorLinker"/> into the persisted maFile.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed class AddAuthenticatorPayload
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

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
}

internal sealed class FinalizeAuthenticatorResponse
{
    [JsonPropertyName("response")]
    public FinalizeAuthenticatorPayload? Response { get; set; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed class FinalizeAuthenticatorPayload
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("server_time")]
    public long ServerTime { get; set; }

    [JsonPropertyName("want_more")]
    public bool WantMore { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
