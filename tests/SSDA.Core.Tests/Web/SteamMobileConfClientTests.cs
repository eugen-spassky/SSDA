using System.Net;
using System.Net.Http;
using System.Text;
using SSDA.Core.Models;
using SSDA.Core.Services;
using SSDA.Core.Web;

namespace SSDA.Core.Tests.Web;

public sealed class SteamMobileConfClientTests
{
    [Fact]
    public async Task ListAsync_parses_react_payload()
    {
        const string json = """
            {
              "success": true,
              "needauth": false,
              "conf": [
                {
                  "type": 2,
                  "type_name": "Trade",
                  "id": "9876543210",
                  "creator_id": "111222333",
                  "nonce": "5555555555",
                  "creation_time": 1700000000,
                  "headline": "Trade with Player1",
                  "summary": ["Knife | Doppler", "+ 5 items"],
                  "icon": "https://example.com/icon.png",
                  "multi": false
                },
                {
                  "type": 3,
                  "type_name": "Market Listing",
                  "id": "1",
                  "creator_id": "0",
                  "nonce": "0",
                  "creation_time": 1700000100,
                  "headline": "Sell Item X for $1.00",
                  "summary": [],
                  "icon": "",
                  "multi": false
                }
              ]
            }
            """;

        var client = BuildClient(json, HttpStatusCode.OK);
        var account = NewAccount();
        var result = await client.ListAsync(account);

        Assert.Equal(2, result.Count);
        Assert.Equal(ConfirmationType.Trade, result[0].Type);
        Assert.Equal(9876543210UL, result[0].Id);
        Assert.Equal(5555555555UL, result[0].Nonce);
        Assert.Equal("Trade with Player1", result[0].Headline);
        Assert.Equal(2, result[0].Summary.Count);
        Assert.Equal(ConfirmationType.MarketListing, result[1].Type);
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_steam_says_no_confirmations()
    {
        const string json = """{"success":false,"needauth":false,"message":"Nothing to confirm."}""";
        var client = BuildClient(json, HttpStatusCode.OK);
        var result = await client.ListAsync(NewAccount());
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_throws_unauthorized_on_needauth()
    {
        const string json = """{"success":false,"needauth":true}""";
        var client = BuildClient(json, HttpStatusCode.OK);
        await Assert.ThrowsAsync<SteamWebUnauthorizedException>(
            () => client.ListAsync(NewAccount()));
    }

    [Fact]
    public async Task ListAsync_throws_unauthorized_on_401()
    {
        var client = BuildClient(string.Empty, HttpStatusCode.Unauthorized);
        await Assert.ThrowsAsync<SteamWebUnauthorizedException>(
            () => client.ListAsync(NewAccount()));
    }

    [Fact]
    public async Task ListAsync_throws_when_identity_secret_missing()
    {
        var client = BuildClient("{}", HttpStatusCode.OK);
        var bad = NewAccount();
        bad.IdentitySecret = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListAsync(bad));
    }

    [Fact]
    public async Task ListAsync_signs_request_with_list_tag()
    {
        const string json = """{"success":true,"conf":[]}""";
        var handler = new RecordingHandler(json, HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var time = new TimeAligner(http, new FixedClock(FixedServerTime));
        var client = new SteamMobileConfClient(http, time);

        await client.ListAsync(NewAccount());

        Assert.NotNull(handler.LastMobileConfRequest);
        var url = handler.LastMobileConfRequest!.RequestUri!.ToString();
        Assert.Contains("/mobileconf/getlist", url);
        Assert.Contains("tag=list", url);
        Assert.Contains("m=react", url);
        Assert.Contains("t=1700000000", url);
        Assert.Contains("p=device-fixture", url);
        Assert.Contains("a=76561198000000000", url);
    }

    private const long FixedServerTime = 1_700_000_000L;

    private static SteamMobileConfClient BuildClient(string body, HttpStatusCode status)
    {
        var handler = new RecordingHandler(body, status);
        var http = new HttpClient(handler);
        var time = new TimeAligner(http, new FixedClock(FixedServerTime));
        return new SteamMobileConfClient(http, time);
    }

    private static SteamGuardAccount NewAccount() => new()
    {
        AccountName = "fixture-user",
        IdentitySecret = "VHIKCgQOFRYWGRoaHB0eHyAhIiMkJSYnKCkqKywtLi8wMQ==",
        DeviceID = "device-fixture",
        Session = new SessionData { SteamID = 76561198000000000UL, AccessToken = "tok" },
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _confBody;
        private readonly HttpStatusCode _confStatus;

        public HttpRequestMessage? LastMobileConfRequest { get; private set; }

        public RecordingHandler(string confBody, HttpStatusCode confStatus)
        {
            _confBody = confBody;
            _confStatus = confStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            HttpResponseMessage msg;
            if (url.Contains("ITwoFactorService/QueryTime", StringComparison.Ordinal))
            {
                var body = "{\"response\":{\"server_time\":\"" + FixedServerTime + "\"}}";
                msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
            }
            else
            {
                LastMobileConfRequest = request;
                msg = new HttpResponseMessage(_confStatus)
                {
                    Content = new StringContent(_confBody, Encoding.UTF8, "application/json"),
                };
            }
            return Task.FromResult(msg);
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        private readonly long _seconds;
        public FixedClock(long s) => _seconds = s;
        public long UtcNowUnixSeconds() => _seconds;
    }
}
