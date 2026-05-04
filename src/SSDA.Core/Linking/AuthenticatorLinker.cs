using System.Net.Http.Json;
using SSDA.Core.Crypto;
using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.Core.Linking;

/// <summary>
/// Drives Steam's mobile authenticator linking endpoints
/// (<c>ITwoFactorService/AddAuthenticator</c> and <c>FinalizeAddAuthenticator</c>).
/// The caller is responsible for obtaining the access token via the login flow first
/// and for adding a verified phone number to the account beforehand — phone-add via
/// the IPhoneService API is intentionally out of scope.
/// </summary>
public sealed class AuthenticatorLinker
{
    private const string AddUrl =
        "https://api.steampowered.com/ITwoFactorService/AddAuthenticator/v1/";
    private const string FinalizeUrl =
        "https://api.steampowered.com/ITwoFactorService/FinalizeAddAuthenticator/v1/";

    private readonly HttpClient _http;
    private readonly TimeAligner _time;

    public AuthenticatorLinker(HttpClient http, TimeAligner time)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Generates a fresh device identifier for a new authenticator. Steam's mobile app
    /// uses the same <c>android:{guid}</c> shape; persisting it lets the same device be
    /// recognised on subsequent confirmations.
    /// </summary>
    public static string GenerateDeviceId() => $"android:{Guid.NewGuid()}";

    /// <summary>
    /// Step 1 of linking: register the new authenticator server-side. On
    /// <see cref="LinkResult.AwaitingFinalization"/> the returned account already has the
    /// freshly-issued <c>shared_secret</c> / <c>identity_secret</c> / <c>revocation_code</c>;
    /// **persist them immediately** before continuing — Steam will only send them once.
    /// </summary>
    public async Task<(LinkResult Result, SteamGuardAccount? Account)> AddAuthenticatorAsync(
        ulong steamId,
        string accessToken,
        string deviceId,
        CancellationToken ct = default)
    {
        if (steamId == 0)
            throw new ArgumentException("steamId must be non-zero.", nameof(steamId));
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentException("accessToken is required.", nameof(accessToken));
        if (string.IsNullOrEmpty(deviceId))
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        var url = AddUrl + "?access_token=" + Uri.EscapeDataString(accessToken);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["steamid"] = steamId.ToString(),
            ["authenticator_type"] = "1",
            ["device_identifier"] = deviceId,
            ["sms_phone_id"] = "1",
            ["version"] = "2",
        });

        AddAuthenticatorPayload? payload;
        using (var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var envelope = await response.Content
                .ReadFromJsonAsync<AddAuthenticatorResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            payload = envelope?.Response;
        }

        if (payload is null) return (LinkResult.GeneralFailure, null);

        return payload.Status switch
        {
            1 => (LinkResult.AwaitingFinalization, BuildAccount(payload, deviceId)),
            2 => (LinkResult.MustProvidePhoneNumber, null),
            29 => (LinkResult.AuthenticatorPresent, null),
            _ => (LinkResult.GeneralFailure, null),
        };
    }

    /// <summary>
    /// Step 2 of linking: confirm with Steam by submitting the SMS code the user received.
    /// Loops up to 10 times if Steam reports clock drift (<c>want_more=true</c>) — each
    /// iteration regenerates a fresh code from the just-issued <c>shared_secret</c>.
    /// </summary>
    public async Task<FinalizeResult> FinalizeAsync(
        ulong steamId,
        string accessToken,
        SteamGuardAccount linkedAccount,
        string smsCode,
        CancellationToken ct = default)
    {
        if (steamId == 0)
            throw new ArgumentException("steamId must be non-zero.", nameof(steamId));
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentException("accessToken is required.", nameof(accessToken));
        ArgumentNullException.ThrowIfNull(linkedAccount);
        if (string.IsNullOrEmpty(linkedAccount.SharedSecret))
            throw new ArgumentException(
                "linkedAccount.SharedSecret is missing — pass the result of AddAuthenticatorAsync.",
                nameof(linkedAccount));
        if (string.IsNullOrEmpty(smsCode))
            throw new ArgumentException("smsCode is required.", nameof(smsCode));

        var url = FinalizeUrl + "?access_token=" + Uri.EscapeDataString(accessToken);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var serverTime = await _time.GetSteamTimeAsync(ct).ConfigureAwait(false);
            var code = SteamTotpGenerator.Generate(linkedAccount.SharedSecret!, serverTime);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["steamid"] = steamId.ToString(),
                ["authenticator_code"] = code,
                ["authenticator_time"] = serverTime.ToString(),
                ["activation_code"] = smsCode,
                ["validate_sms_code"] = "1",
            });

            FinalizeAuthenticatorPayload? payload;
            using (var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var envelope = await response.Content
                    .ReadFromJsonAsync<FinalizeAuthenticatorResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);
                payload = envelope?.Response;
            }

            if (payload is null) return FinalizeResult.GeneralFailure;
            if (payload.Status == 89) return FinalizeResult.BadSMSCode;
            if (payload.Status == 88 && attempt >= 9) return FinalizeResult.UnableToGenerateCorrectCodes;
            if (!payload.Success && !payload.WantMore) return FinalizeResult.GeneralFailure;
            if (payload.WantMore) continue;

            linkedAccount.FullyEnrolled = true;
            return FinalizeResult.Success;
        }

        return FinalizeResult.UnableToGenerateCorrectCodes;
    }

    private static SteamGuardAccount BuildAccount(AddAuthenticatorPayload payload, string deviceId) =>
        new()
        {
            SharedSecret = payload.SharedSecret,
            SerialNumber = payload.SerialNumber,
            RevocationCode = payload.RevocationCode,
            URI = payload.URI,
            ServerTime = payload.ServerTime,
            AccountName = payload.AccountName,
            TokenGID = payload.TokenGID,
            IdentitySecret = payload.IdentitySecret,
            Secret1 = payload.Secret1,
            Status = payload.Status,
            DeviceID = deviceId,
            FullyEnrolled = false,
        };
}
