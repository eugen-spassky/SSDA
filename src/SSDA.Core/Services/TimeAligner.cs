using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SSDA.Core.Services;

/// <summary>
/// Aligns the local clock with Steam's mobile API server clock. The first call probes the
/// <c>ITwoFactorService/QueryTime</c> endpoint and caches the offset; subsequent calls add
/// that offset to the local clock. Codes generated with a 1+ second drift will be rejected
/// by Steam, so this alignment is essential.
/// </summary>
public sealed class TimeAligner
{
    private const string QueryTimeUrl =
        "https://api.steampowered.com/ITwoFactorService/QueryTime/v1/";

    private readonly HttpClient _http;
    private readonly ISystemClock _clock;
    private long _offsetSeconds;
    private bool _aligned;

    /// <summary>Initialises the aligner. <paramref name="http"/> may be a shared client.</summary>
    public TimeAligner(HttpClient http, ISystemClock? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _clock = clock ?? SystemClock.Instance;
    }

    /// <summary>True once <see cref="AlignAsync"/> succeeded at least once.</summary>
    public bool IsAligned => _aligned;

    /// <summary>The last computed offset in seconds (server - local).</summary>
    public long OffsetSeconds => _offsetSeconds;

    /// <summary>Returns the best estimate of Steam's current unix time.</summary>
    public async ValueTask<long> GetSteamTimeAsync(CancellationToken ct = default)
    {
        if (!_aligned)
            await AlignAsync(ct).ConfigureAwait(false);
        return _clock.UtcNowUnixSeconds() + _offsetSeconds;
    }

    /// <summary>
    /// Refreshes the cached offset by calling <c>QueryTime</c>. Safe to call repeatedly.
    /// </summary>
    public async Task AlignAsync(CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("steamid", "0") });

        using var response = await _http.PostAsync(QueryTimeUrl, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<QueryTimeEnvelope>(cancellationToken: ct)
            .ConfigureAwait(false);

        var serverTime = payload?.Response?.ServerTime
            ?? throw new InvalidOperationException("Steam QueryTime returned no server_time.");

        _offsetSeconds = serverTime - _clock.UtcNowUnixSeconds();
        _aligned = true;
    }

    private sealed class QueryTimeEnvelope
    {
        [JsonPropertyName("response")]
        public QueryTimeResponse? Response { get; set; }
    }

    private sealed class QueryTimeResponse
    {
        [JsonPropertyName("server_time")]
        public long ServerTime { get; set; }

        [JsonPropertyName("skew_tolerance_seconds")]
        public long SkewToleranceSeconds { get; set; }

        [JsonPropertyName("large_time_jink")]
        public long LargeTimeJink { get; set; }

        [JsonPropertyName("probe_frequency_seconds")]
        public long ProbeFrequencySeconds { get; set; }

        [JsonPropertyName("adjusted_time_probe_frequency_seconds")]
        public long AdjustedTimeProbeFrequencySeconds { get; set; }

        [JsonPropertyName("hint_probe_frequency_seconds")]
        public long HintProbeFrequencySeconds { get; set; }

        [JsonPropertyName("sync_timeout")]
        public long SyncTimeout { get; set; }

        [JsonPropertyName("try_again_seconds")]
        public long TryAgainSeconds { get; set; }

        [JsonPropertyName("max_attempts")]
        public long MaxAttempts { get; set; }
    }
}
