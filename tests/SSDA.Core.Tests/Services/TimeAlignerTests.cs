using System.Net;
using System.Net.Http;
using System.Text;
using SSDA.Core.Services;

namespace SSDA.Core.Tests.Services;

public sealed class TimeAlignerTests
{
    [Fact]
    public async Task AlignAsync_parses_string_encoded_numeric_fields()
    {
        // Steam returns every numeric field as a JSON string; AlignAsync must accept that shape.
        var response = """
            {
              "response": {
                "server_time": "1700000123",
                "skew_tolerance_seconds": "60",
                "large_time_jink": "86400",
                "probe_frequency_seconds": "3600",
                "adjusted_time_probe_frequency_seconds": "300",
                "hint_probe_frequency_seconds": "60",
                "sync_timeout": "60",
                "try_again_seconds": "900",
                "max_attempts": "3"
              }
            }
            """;
        var http = new HttpClient(new StubHandler(response));
        var clock = new FixedClock(unixSeconds: 1_700_000_000L);
        var aligner = new TimeAligner(http, clock);

        await aligner.AlignAsync();

        Assert.True(aligner.IsAligned);
        Assert.Equal(123L, aligner.OffsetSeconds);
    }

    [Fact]
    public async Task GetSteamTimeAsync_returns_server_time_minus_clock_drift()
    {
        var response = """{"response":{"server_time":"2000000000"}}""";
        var http = new HttpClient(new StubHandler(response));
        var clock = new FixedClock(unixSeconds: 1_999_999_990L);
        var aligner = new TimeAligner(http, clock);

        var steamTime = await aligner.GetSteamTimeAsync();

        Assert.Equal(2_000_000_000L, steamTime);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(msg);
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        private readonly long _seconds;

        public FixedClock(long unixSeconds) => _seconds = unixSeconds;

        public long UtcNowUnixSeconds() => _seconds;
    }
}
