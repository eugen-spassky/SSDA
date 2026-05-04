using System.Net;
using System.Net.Http;

namespace SSDA.Core.Web;

/// <summary>
/// Holds a per-account cookie jar for steamcommunity.com mobile endpoints. The
/// caller supplies the SteamID and the OAuth access token; the rest of the
/// cookies (mobileClient flag, sessionid, language) are static.
/// </summary>
public sealed class SteamWebSession
{
    public const string SteamCommunityHost = "steamcommunity.com";

    private readonly CookieContainer _cookies;
    private readonly string _sessionId;

    public SteamWebSession(ulong steamId, string accessToken, string? sessionId = null)
    {
        if (steamId == 0)
            throw new ArgumentOutOfRangeException(nameof(steamId), "SteamID must be non-zero.");
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        SteamId = steamId;
        AccessToken = accessToken;
        _sessionId = sessionId ?? GenerateSessionId();
        _cookies = BuildCookies();
    }

    public ulong SteamId { get; }
    public string AccessToken { get; }
    public string SessionId => _sessionId;
    public CookieContainer Cookies => _cookies;

    /// <summary>
    /// Builds a <see cref="HttpClientHandler"/> wired up with this session's cookie
    /// jar so the same cookies are shared by every request through the client.
    /// </summary>
    public HttpClientHandler CreateHandler() => new()
    {
        UseCookies = true,
        CookieContainer = _cookies,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    };

    private CookieContainer BuildCookies()
    {
        var jar = new CookieContainer();
        var domain = $".{SteamCommunityHost}";
        var path = "/";

        var encodedToken = WebUtility.UrlEncode(AccessToken);
        jar.Add(new Cookie("steamLoginSecure", $"{SteamId}||{encodedToken}", path, domain));
        jar.Add(new Cookie("sessionid", _sessionId, path, domain));
        jar.Add(new Cookie("mobileClient", "android", path, domain));
        jar.Add(new Cookie("mobileClientVersion", "777777 3.0.0", path, domain));
        jar.Add(new Cookie("Steam_Language", "english", path, domain));
        jar.Add(new Cookie("dob", string.Empty, path, domain));
        return jar;
    }

    private static string GenerateSessionId()
    {
        Span<byte> bytes = stackalloc byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
