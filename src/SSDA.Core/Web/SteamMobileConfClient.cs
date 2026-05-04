using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.Core.Web;

/// <summary>
/// Pulls the list of pending mobile confirmations for an account. Read-only:
/// accept/deny ships in PR&#160;#3.
/// </summary>
public sealed class SteamMobileConfClient
{
    private const string MobileConfBase = "https://steamcommunity.com/mobileconf";

    private readonly HttpClient _http;
    private readonly TimeAligner _time;

    public SteamMobileConfClient(HttpClient http, TimeAligner time)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Fetches every pending confirmation for the given account. Returns an empty
    /// list when Steam reports <c>success=false</c> with the standard
    /// «No confirmations» payload, and throws otherwise.
    /// </summary>
    public async Task<IReadOnlyList<Confirmation>> ListAsync(
        SteamGuardAccount account,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrEmpty(account.IdentitySecret))
            throw new InvalidOperationException("Account is missing identity_secret.");
        if (string.IsNullOrEmpty(account.DeviceID))
            throw new InvalidOperationException("Account is missing device_id.");

        var time = await _time.GetSteamTimeAsync(ct).ConfigureAwait(false);
        var tag = MobileConfTagSigner.Sign(account.IdentitySecret, time, "list");

        var url =
            $"{MobileConfBase}/getlist" +
            $"?p={WebUtility.UrlEncode(account.DeviceID)}" +
            $"&a={account.Session?.SteamID}" +
            $"&k={tag}" +
            $"&t={time}" +
            $"&m=react" +
            $"&tag=list";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Linux; U; Android 9; en-us; Steam App 3.0.0)");
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new SteamWebUnauthorizedException("Steam rejected the access token (401).");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<GetListResponse>(cancellationToken: ct)
            .ConfigureAwait(false);

        if (payload is null)
            throw new InvalidOperationException("Steam returned an empty mobileconf payload.");

        if (payload.NeedAuth)
            throw new SteamWebUnauthorizedException("Steam needs reauthentication.");

        if (!payload.Success)
            return Array.Empty<Confirmation>();

        return Map(payload.Conf);
    }

    private static IReadOnlyList<Confirmation> Map(IEnumerable<ConfirmationDto>? items)
    {
        if (items is null) return Array.Empty<Confirmation>();
        var output = new List<Confirmation>();
        foreach (var dto in items)
        {
            output.Add(new Confirmation
            {
                Id = ParseUlong(dto.Id),
                Nonce = ParseUlong(dto.Nonce),
                CreatorId = ParseUlong(dto.CreatorId),
                Headline = dto.Headline,
                Summary = dto.Summary ?? new List<string>(),
                IconUrl = dto.Icon,
                Type = MapType(dto.Type),
                TypeName = dto.TypeName,
                CreationTime = dto.CreationTime,
            });
        }
        return output;
    }

    private static ulong ParseUlong(string? value) =>
        ulong.TryParse(value, out var v) ? v : 0UL;

    private static ConfirmationType MapType(int raw) =>
        Enum.IsDefined(typeof(ConfirmationType), raw)
            ? (ConfirmationType)raw
            : ConfirmationType.Unknown;
}

/// <summary>Steam returned 401 / needauth — the access token is no longer valid.</summary>
public sealed class SteamWebUnauthorizedException : Exception
{
    public SteamWebUnauthorizedException(string message) : base(message) { }
}
