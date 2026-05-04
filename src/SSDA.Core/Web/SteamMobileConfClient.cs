using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.Core.Web;

/// <summary>
/// Reads pending mobile confirmations and accepts/denies them via Steam's
/// <c>/mobileconf/ajaxop</c> + <c>/mobileconf/multiajaxop</c> endpoints.
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
        var session = ValidateAccount(account);

        var time = await _time.GetSteamTimeAsync(ct).ConfigureAwait(false);
        var tag = MobileConfTagSigner.Sign(account.IdentitySecret!, time, "list");

        var url =
            $"{MobileConfBase}/getlist" +
            $"?p={WebUtility.UrlEncode(account.DeviceID)}" +
            $"&a={session.SteamID}" +
            $"&k={WebUtility.UrlEncode(tag)}" +
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

    /// <summary>Approves a single confirmation (Allow / Accept).</summary>
    public Task AcceptAsync(
        SteamGuardAccount account, Confirmation confirmation, CancellationToken ct = default)
        => SingleAjaxAsync(account, confirmation, ConfirmationOp.Allow, ct);

    /// <summary>Denies a single confirmation (Cancel / Reject).</summary>
    public Task DenyAsync(
        SteamGuardAccount account, Confirmation confirmation, CancellationToken ct = default)
        => SingleAjaxAsync(account, confirmation, ConfirmationOp.Cancel, ct);

    /// <summary>Approves multiple confirmations in a single batched request.</summary>
    public Task AcceptManyAsync(
        SteamGuardAccount account,
        IReadOnlyCollection<Confirmation> confirmations,
        CancellationToken ct = default)
        => MultiAjaxAsync(account, confirmations, ConfirmationOp.Allow, ct);

    /// <summary>Denies multiple confirmations in a single batched request.</summary>
    public Task DenyManyAsync(
        SteamGuardAccount account,
        IReadOnlyCollection<Confirmation> confirmations,
        CancellationToken ct = default)
        => MultiAjaxAsync(account, confirmations, ConfirmationOp.Cancel, ct);

    private async Task SingleAjaxAsync(
        SteamGuardAccount account,
        Confirmation confirmation,
        ConfirmationOp op,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        var session = ValidateAccount(account);
        var time = await _time.GetSteamTimeAsync(ct).ConfigureAwait(false);
        var opTag = OpTag(op);
        var k = MobileConfTagSigner.Sign(account.IdentitySecret!, time, opTag);

        var url =
            $"{MobileConfBase}/ajaxop" +
            $"?op={opTag}" +
            $"&p={WebUtility.UrlEncode(account.DeviceID)}" +
            $"&a={session.SteamID}" +
            $"&k={WebUtility.UrlEncode(k)}" +
            $"&t={time}" +
            $"&m=react" +
            $"&tag={opTag}" +
            $"&cid={confirmation.Id}" +
            $"&ck={confirmation.Nonce}";

        using var req = BuildAjaxRequest(HttpMethod.Get, url);
        await SendAjaxAndCheckAsync(req, ct).ConfigureAwait(false);
    }

    private async Task MultiAjaxAsync(
        SteamGuardAccount account,
        IReadOnlyCollection<Confirmation> confirmations,
        ConfirmationOp op,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(confirmations);
        if (confirmations.Count == 0) return;

        var session = ValidateAccount(account);
        var time = await _time.GetSteamTimeAsync(ct).ConfigureAwait(false);
        var opTag = OpTag(op);
        var k = MobileConfTagSigner.Sign(account.IdentitySecret!, time, opTag);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("op", opTag),
            new("p", account.DeviceID!),
            new("a", session.SteamID.ToString()),
            new("k", k),
            new("t", time.ToString()),
            new("m", "react"),
            new("tag", opTag),
        };
        foreach (var c in confirmations)
        {
            fields.Add(new("cid[]", c.Id.ToString()));
            fields.Add(new("ck[]", c.Nonce.ToString()));
        }

        using var req = BuildAjaxRequest(HttpMethod.Post, $"{MobileConfBase}/multiajaxop");
        req.Content = new FormUrlEncodedContent(fields);
        await SendAjaxAndCheckAsync(req, ct).ConfigureAwait(false);
    }

    private async Task SendAjaxAndCheckAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new SteamWebUnauthorizedException("Steam rejected the access token (401).");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<AjaxResponse>(cancellationToken: ct)
            .ConfigureAwait(false);

        if (payload is null)
            throw new InvalidOperationException("Steam returned an empty ajax payload.");
        if (payload.NeedAuth)
            throw new SteamWebUnauthorizedException("Steam needs reauthentication.");
        if (!payload.Success)
            throw new SteamConfirmationException(
                string.IsNullOrWhiteSpace(payload.Message)
                    ? "Steam refused the confirmation operation."
                    : payload.Message!);
    }

    private static HttpRequestMessage BuildAjaxRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Linux; U; Android 9; en-us; Steam App 3.0.0)");
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Referrer = new Uri("https://steamcommunity.com/mobileconf/conf");
        req.Headers.Add("X-Requested-With", "com.valvesoftware.android.steam.community");
        return req;
    }

    private static SessionData ValidateAccount(SteamGuardAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrEmpty(account.IdentitySecret))
            throw new InvalidOperationException("Account is missing identity_secret.");
        if (string.IsNullOrEmpty(account.DeviceID))
            throw new InvalidOperationException("Account is missing device_id.");
        var session = account.Session
            ?? throw new InvalidOperationException("Account is missing a Session.");
        if (session.SteamID == 0)
            throw new InvalidOperationException("Account session has no SteamID.");
        return session;
    }

    private static string OpTag(ConfirmationOp op) => op switch
    {
        ConfirmationOp.Allow => "allow",
        ConfirmationOp.Cancel => "cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private enum ConfirmationOp { Allow, Cancel }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    private sealed class AjaxResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("needauth")]
        public bool NeedAuth { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}

/// <summary>Steam refused an accept/deny call (response had <c>success=false</c>).</summary>
public sealed class SteamConfirmationException : Exception
{
    public SteamConfirmationException(string message) : base(message) { }
}

/// <summary>Steam returned 401 / needauth — the access token is no longer valid.</summary>
public sealed class SteamWebUnauthorizedException : Exception
{
    public SteamWebUnauthorizedException(string message) : base(message) { }
}
