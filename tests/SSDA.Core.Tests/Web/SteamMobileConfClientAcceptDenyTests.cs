using System.Net;
using System.Net.Http;
using System.Text;
using SSDA.Core.Models;
using SSDA.Core.Services;
using SSDA.Core.Web;

namespace SSDA.Core.Tests.Web;

public sealed class SteamMobileConfClientAcceptDenyTests
{
    private const long FixedServerTime = 1_700_000_000L;

    [Fact]
    public async Task AcceptAsync_calls_ajaxop_with_op_allow()
    {
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.AcceptAsync(NewAccount(), NewConfirmation(id: 11, nonce: 22));

        var req = handler.LastAjaxRequest!;
        Assert.Equal(HttpMethod.Get, req.Method);
        var url = req.RequestUri!.ToString();
        Assert.Contains("/mobileconf/ajaxop", url);
        Assert.Contains("op=allow", url);
        Assert.Contains("tag=allow", url);
        Assert.Contains("cid=11", url);
        Assert.Contains("ck=22", url);
    }

    [Fact]
    public async Task DenyAsync_calls_ajaxop_with_op_cancel()
    {
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.DenyAsync(NewAccount(), NewConfirmation(id: 33, nonce: 44));

        var req = handler.LastAjaxRequest!;
        var url = req.RequestUri!.ToString();
        Assert.Contains("op=cancel", url);
        Assert.Contains("tag=cancel", url);
        Assert.Contains("cid=33", url);
        Assert.Contains("ck=44", url);
    }

    [Fact]
    public async Task AcceptAsync_throws_when_steam_returns_success_false()
    {
        var handler = NewHandler(
            """{"success":false,"message":"Invalid auth"}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        var ex = await Assert.ThrowsAsync<SteamConfirmationException>(
            () => client.AcceptAsync(NewAccount(), NewConfirmation()));
        Assert.Contains("Invalid auth", ex.Message);
    }

    [Fact]
    public async Task AcceptAsync_throws_unauthorized_on_needauth()
    {
        var handler = NewHandler("""{"success":false,"needauth":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await Assert.ThrowsAsync<SteamWebUnauthorizedException>(
            () => client.AcceptAsync(NewAccount(), NewConfirmation()));
    }

    [Fact]
    public async Task AcceptAsync_throws_unauthorized_on_401()
    {
        var handler = NewHandler(string.Empty, HttpStatusCode.Unauthorized);
        var client = NewClient(handler);

        await Assert.ThrowsAsync<SteamWebUnauthorizedException>(
            () => client.AcceptAsync(NewAccount(), NewConfirmation()));
    }

    [Fact]
    public async Task AcceptManyAsync_posts_form_with_cid_array()
    {
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.AcceptManyAsync(
            NewAccount(),
            new[] { NewConfirmation(id: 1, nonce: 2), NewConfirmation(id: 3, nonce: 4) });

        var req = handler.LastAjaxRequest!;
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal(
            "https://steamcommunity.com/mobileconf/multiajaxop",
            req.RequestUri!.ToString());

        var body = await req.Content!.ReadAsStringAsync();
        Assert.Contains("op=allow", body);
        Assert.Contains("tag=allow", body);
        Assert.Contains("m=react", body);
        Assert.Contains("cid%5B%5D=1", body); // cid[]=1, urlencoded
        Assert.Contains("ck%5B%5D=2", body);
        Assert.Contains("cid%5B%5D=3", body);
        Assert.Contains("ck%5B%5D=4", body);
    }

    [Fact]
    public async Task AcceptManyAsync_does_not_double_encode_signature()
    {
        // Regression: FormUrlEncodedContent already URL-encodes its values, so the
        // signature must be passed in raw base64 form. If MobileConfTagSigner pre-encoded
        // the value, the form body would double-encode (e.g. '=' -> '%3D' -> '%253D')
        // and Steam would reject every multiajaxop request with a signature mismatch.
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.AcceptManyAsync(NewAccount(), new[] { NewConfirmation(id: 7, nonce: 8) });

        var body = await handler.LastAjaxRequest!.Content!.ReadAsStringAsync();
        var k = ExtractFormValue(body, "k");
        Assert.False(string.IsNullOrEmpty(k));
        Assert.DoesNotContain("%25", k); // double-encoded '%' would appear here
        // The decoded value must round-trip to a valid 20-byte SHA-1 hash.
        var decoded = Convert.FromBase64String(k!);
        Assert.Equal(20, decoded.Length);
    }

    private static string? ExtractFormValue(string body, string name)
    {
        foreach (var pair in body.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = WebUtility.UrlDecode(pair[..eq]);
            if (key == name) return WebUtility.UrlDecode(pair[(eq + 1)..]);
        }
        return null;
    }

    [Fact]
    public async Task AcceptManyAsync_skips_request_for_empty_input()
    {
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.AcceptManyAsync(NewAccount(), Array.Empty<Confirmation>());

        Assert.Null(handler.LastAjaxRequest);
    }

    [Fact]
    public async Task DenyManyAsync_posts_with_op_cancel()
    {
        var handler = NewHandler("""{"success":true}""", HttpStatusCode.OK);
        var client = NewClient(handler);

        await client.DenyManyAsync(NewAccount(), new[] { NewConfirmation(id: 1, nonce: 2) });

        var body = await handler.LastAjaxRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("op=cancel", body);
        Assert.Contains("tag=cancel", body);
    }

    [Fact]
    public async Task AcceptAsync_throws_when_session_missing()
    {
        var handler = NewHandler("{}", HttpStatusCode.OK);
        var client = NewClient(handler);
        var bad = NewAccount();
        bad.Session = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.AcceptAsync(bad, NewConfirmation()));
    }

    private static SteamMobileConfClient NewClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler);
        var time = new TimeAligner(http, new FixedClock(FixedServerTime));
        return new SteamMobileConfClient(http, time);
    }

    private static RecordingHandler NewHandler(string body, HttpStatusCode status) =>
        new(body, status);

    private static SteamGuardAccount NewAccount() => new()
    {
        AccountName = "fixture",
        IdentitySecret = "VHIKCgQOFRYWGRoaHB0eHyAhIiMkJSYnKCkqKywtLi8wMQ==",
        DeviceID = "device-fixture",
        Session = new SessionData { SteamID = 76561198000000000UL, AccessToken = "tok" },
    };

    private static Confirmation NewConfirmation(ulong id = 1, ulong nonce = 1) => new()
    {
        Id = id,
        Nonce = nonce,
        Type = ConfirmationType.Trade,
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public HttpMethod? LastAjaxMethod { get; private set; }
        public Uri? LastAjaxUri { get; private set; }
        public string? LastAjaxBody { get; private set; }
        public HttpRequestMessage? LastAjaxRequest =>
            LastAjaxMethod is null ? null : new SnapshotRequest(this);

        public RecordingHandler(string body, HttpStatusCode status)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("ITwoFactorService/QueryTime", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"response\":{\"server_time\":\"" + FixedServerTime + "\"}}",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            LastAjaxMethod = request.Method;
            LastAjaxUri = request.RequestUri;
            LastAjaxBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }

        // Compatibility shim: older tests still inspect the request via .Method / .RequestUri / .Content
        private sealed class SnapshotRequest : HttpRequestMessage
        {
            public SnapshotRequest(RecordingHandler h)
            {
                Method = h.LastAjaxMethod!;
                RequestUri = h.LastAjaxUri;
                if (h.LastAjaxBody is not null)
                    Content = new StringContent(h.LastAjaxBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            }
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        private readonly long _seconds;
        public FixedClock(long s) => _seconds = s;
        public long UtcNowUnixSeconds() => _seconds;
    }
}
