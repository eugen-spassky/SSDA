using System.Text.Json.Serialization;

namespace SSDA.Core.Models;

/// <summary>
/// Steam mobile session for an authenticated account. Mirrors the layout used by
/// <c>jessecar96/SteamDesktopAuthenticator</c> so existing <c>.maFile</c> documents
/// load without conversion.
/// </summary>
public sealed class SessionData
{
    [JsonPropertyName("SteamID")]
    public ulong SteamID { get; set; }

    [JsonPropertyName("AccessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("RefreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("SessionID")]
    public string? SessionID { get; set; }
}
