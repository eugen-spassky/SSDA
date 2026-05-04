using System.Net;
using System.Net.Http;
using System.Text;
using SSDA.Core.Linking;
using SSDA.Core.Models;
using SSDA.Core.Services;
using Xunit;

namespace SSDA.Core.Tests.Linking;

public class AuthenticatorLinkerTests
{
    private const string ValidAccessToken = "eyJhbGciOiJ.access.token";
    private const string SharedSecret = "RGFsbGFzIENvd2JveXMgYXJlIE5vMQ==";

    [Fact]
    public void GenerateDeviceId_returns_android_prefixed_guid()
    {
        var id = AuthenticatorLinker.GenerateDeviceId();

        Assert.StartsWith("android:", id);
        Assert.True(Guid.TryParse(id.Substring("android:".Length), out _));
    }

    [Fact]
    public void GenerateDeviceId_returns_unique_values_per_call()
    {
        Assert.NotEqual(
            AuthenticatorLinker.GenerateDeviceId(),
            AuthenticatorLinker.GenerateDeviceId());
    }

    [Fact]
    public async Task AddAuthenticatorAsync_returns_AwaitingFinalization_on_status_1()
    {
        const string body = """
        {
            "response": {
                "status": 1,
                "shared_secret": "Tg+0xS0vmUOBDe2vUd4kupbS5jc=",
                "serial_number": "12345",
                "revocation_code": "R12345",
                "uri": "otpauth://totp/Steam:user?secret=...",
                "server_time": "1700000000",
                "account_name": "user",
                "token_gid": "abcdef",
                "identity_secret": "AbcdEFGhij1234567890=",
                "secret_1": "+secret1+="
            }
        }
        """;
        var (linker, _) = NewLinker(body);

        var (result, account) = await linker.AddAuthenticatorAsync(
            76561198000000000UL, ValidAccessToken, "android:abc-123");

        Assert.Equal(LinkResult.AwaitingFinalization, result);
        Assert.NotNull(account);
        Assert.Equal("Tg+0xS0vmUOBDe2vUd4kupbS5jc=", account!.SharedSecret);
        Assert.Equal("R12345", account.RevocationCode);
        Assert.Equal("AbcdEFGhij1234567890=", account.IdentitySecret);
        Assert.Equal("android:abc-123", account.DeviceID);
        Assert.False(account.FullyEnrolled);
    }

    [Fact]
    public async Task AddAuthenticatorAsync_returns_MustProvidePhoneNumber_on_status_2()
    {
        var (linker, _) = NewLinker("""{"response":{"status":2}}""");

        var (result, account) = await linker.AddAuthenticatorAsync(
            76561198000000000UL, ValidAccessToken, "android:abc");

        Assert.Equal(LinkResult.MustProvidePhoneNumber, result);
        Assert.Null(account);
    }

    [Fact]
    public async Task AddAuthenticatorAsync_returns_AuthenticatorPresent_on_status_29()
    {
        var (linker, _) = NewLinker("""{"response":{"status":29}}""");

        var (result, _) = await linker.AddAuthenticatorAsync(
            76561198000000000UL, ValidAccessToken, "android:abc");

        Assert.Equal(LinkResult.AuthenticatorPresent, result);
    }

    [Fact]
    public async Task AddAuthenticatorAsync_returns_GeneralFailure_on_unknown_status()
    {
        var (linker, _) = NewLinker("""{"response":{"status":99}}""");

        var (result, _) = await linker.AddAuthenticatorAsync(
            76561198000000000UL, ValidAccessToken, "android:abc");

        Assert.Equal(LinkResult.GeneralFailure, result);
    }

    [Fact]
    public async Task AddAuthenticatorAsync_passes_access_token_in_query_string()
    {
        var (linker, recording) = NewLinker("""{"response":{"status":2}}""");

        await linker.AddAuthenticatorAsync(
            76561198000000000UL, ValidAccessToken, "android:abc");

        var lastRequest = recording.LastRequestUrl;
        Assert.Contains("access_token=", lastRequest);
        Assert.Contains(Uri.EscapeDataString(ValidAccessToken), lastRequest);
    }

    [Theory]
    [InlineData(0UL, ValidAccessToken, "android:abc")]
    public async Task AddAuthenticatorAsync_throws_on_zero_steam_id(
        ulong steamId, string token, string deviceId)
    {
        var (linker, _) = NewLinker("");

        await Assert.ThrowsAsync<ArgumentException>(
            () => linker.AddAuthenticatorAsync(steamId, token, deviceId));
    }

    [Fact]
    public async Task AddAuthenticatorAsync_throws_on_empty_access_token()
    {
        var (linker, _) = NewLinker("");

        await Assert.ThrowsAsync<ArgumentException>(
            () => linker.AddAuthenticatorAsync(76561198000000000UL, "", "android:abc"));
    }

    [Fact]
    public async Task AddAuthenticatorAsync_throws_on_empty_device_id()
    {
        var (linker, _) = NewLinker("");

        await Assert.ThrowsAsync<ArgumentException>(
            () => linker.AddAuthenticatorAsync(76561198000000000UL, ValidAccessToken, ""));
    }

    [Fact]
    public async Task FinalizeAsync_returns_Success_on_first_attempt()
    {
        var (linker, recording) = NewLinker(
            """{"response":{"status":1,"server_time":"1700000000","success":true,"want_more":false}}""");
        var account = new SteamGuardAccount { SharedSecret = SharedSecret };

        var result = await linker.FinalizeAsync(
            76561198000000000UL, ValidAccessToken, account, "ABCDE");

        Assert.Equal(FinalizeResult.Success, result);
        Assert.True(account.FullyEnrolled);
        Assert.Single(recording.RequestBodies);
        Assert.Contains("activation_code=ABCDE", recording.RequestBodies[0]);
    }

    [Fact]
    public async Task FinalizeAsync_returns_BadSMSCode_on_status_89()
    {
        var (linker, _) = NewLinker("""{"response":{"status":89}}""");
        var account = new SteamGuardAccount { SharedSecret = SharedSecret };

        var result = await linker.FinalizeAsync(
            76561198000000000UL, ValidAccessToken, account, "WRONG");

        Assert.Equal(FinalizeResult.BadSMSCode, result);
        Assert.False(account.FullyEnrolled);
    }

    [Fact]
    public async Task FinalizeAsync_retries_when_want_more_then_succeeds()
    {
        var bodies = new Queue<string>(new[]
        {
            """{"response":{"want_more":true,"success":false}}""",
            """{"response":{"want_more":true,"success":false}}""",
            """{"response":{"want_more":false,"success":true}}""",
        });
        var (linker, recording) = NewLinker(bodies);
        var account = new SteamGuardAccount { SharedSecret = SharedSecret };

        var result = await linker.FinalizeAsync(
            76561198000000000UL, ValidAccessToken, account, "ABCDE");

        Assert.Equal(FinalizeResult.Success, result);
        Assert.True(account.FullyEnrolled);
        Assert.Equal(3, recording.RequestBodies.Count);
    }

    [Fact]
    public async Task FinalizeAsync_returns_UnableToGenerateCorrectCodes_after_10_status_88()
    {
        // Status 88 = clock drift; Steam sets want_more=true on every iteration so we keep
        // regenerating codes. After 10 unsuccessful attempts we surface the failure.
        var bodies = new Queue<string>(
            Enumerable.Repeat("""{"response":{"status":88,"want_more":true,"success":false}}""", 10));
        var (linker, recording) = NewLinker(bodies);
        var account = new SteamGuardAccount { SharedSecret = SharedSecret };

        var result = await linker.FinalizeAsync(
            76561198000000000UL, ValidAccessToken, account, "ABCDE");

        Assert.Equal(FinalizeResult.UnableToGenerateCorrectCodes, result);
        Assert.False(account.FullyEnrolled);
    }

    [Fact]
    public async Task FinalizeAsync_throws_on_missing_shared_secret()
    {
        var (linker, _) = NewLinker("");
        var account = new SteamGuardAccount();

        await Assert.ThrowsAsync<ArgumentException>(
            () => linker.FinalizeAsync(76561198000000000UL, ValidAccessToken, account, "ABCDE"));
    }

    private static (AuthenticatorLinker linker, RecordingHandler handler) NewLinker(string body)
    {
        var handler = new RecordingHandler(body);
        var http = new HttpClient(handler);
        // TimeAligner queries another endpoint; intercept that too if needed.
        var time = new TimeAligner(http, new FixedClock(1_700_000_000L));
        // Pre-mark TimeAligner aligned by calling Align with the same handler — easier: stub it.
        // The handler returns the same body for every URL — good enough since we only inspect
        // the linker request, and TimeAligner happily parses any JSON with server_time.
        return (new AuthenticatorLinker(http, time), handler);
    }

    private static (AuthenticatorLinker linker, RecordingHandler handler) NewLinker(Queue<string> bodies)
    {
        var handler = new RecordingHandler(bodies);
        var http = new HttpClient(handler);
        var time = new TimeAligner(http, new FixedClock(1_700_000_000L));
        return (new AuthenticatorLinker(http, time), handler);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> _bodies;
        public List<string> RequestBodies { get; } = new();
        public string? LastRequestUrl { get; private set; }

        public RecordingHandler(string body) => _bodies = new Queue<string>(new[] { body });
        public RecordingHandler(Queue<string> bodies) => _bodies = bodies;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TimeAligner first call uses the same handler; respond with a QueryTime body
            // and skip recording it so tests can assert on linker requests only.
            var url = request.RequestUri?.ToString() ?? "";
            string responseBody;
            if (url.Contains("ITwoFactorService/QueryTime"))
            {
                responseBody = """{"response":{"server_time":"1700000000"}}""";
            }
            else
            {
                LastRequestUrl = url;
                if (request.Content is not null)
                    RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                responseBody = _bodies.Count > 0 ? _bodies.Dequeue() : """{"response":{}}""";
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        private readonly long _seconds;
        public FixedClock(long unixSeconds) => _seconds = unixSeconds;
        public long UtcNowUnixSeconds() => _seconds;
    }
}
