using System.Net;
using System.Net.Http;
using System.Text;
using SSDA.Core.Auth;
using SSDA.Core.Crypto;
using SSDA.Core.Services;
using Xunit;

namespace SSDA.Core.Tests.Auth;

public class SharedSecretAuthenticatorTests
{
    private const string SharedSecret = "RGFsbGFzIENvd2JveXMgYXJlIE5vMQ==";

    [Fact]
    public void Ctor_throws_when_shared_secret_is_empty()
    {
        var time = NewTimeAligner();
        Assert.Throws<ArgumentException>(() => new SharedSecretAuthenticator("", time));
        Assert.Throws<ArgumentException>(() => new SharedSecretAuthenticator(null!, time));
    }

    [Fact]
    public void Ctor_throws_when_time_aligner_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SharedSecretAuthenticator(SharedSecret, null!));
    }

    [Fact]
    public async Task GetDeviceCodeAsync_returns_totp_for_shared_secret_at_aligned_time()
    {
        const long fixedSteamTime = 1_700_000_000L;
        var time = NewTimeAligner(serverTime: fixedSteamTime, localTime: fixedSteamTime - 5);
        var auth = new SharedSecretAuthenticator(SharedSecret, time);

        var code = await auth.GetDeviceCodeAsync(false);

        var expected = SteamTotpGenerator.Generate(SharedSecret, fixedSteamTime);
        Assert.Equal(expected, code);
        Assert.Equal(5, code.Length);
    }

    [Fact]
    public async Task GetDeviceCodeAsync_throws_when_previous_code_was_incorrect()
    {
        var auth = new SharedSecretAuthenticator(SharedSecret, NewTimeAligner());

        await Assert.ThrowsAsync<SteamLoginException>(
            () => auth.GetDeviceCodeAsync(true));
    }

    [Fact]
    public async Task GetEmailCodeAsync_throws_SteamLoginException()
    {
        var auth = new SharedSecretAuthenticator(SharedSecret, NewTimeAligner());

        await Assert.ThrowsAsync<SteamLoginException>(
            () => auth.GetEmailCodeAsync("user@example.com", false));
    }

    [Fact]
    public async Task AcceptDeviceConfirmationAsync_returns_false_so_steam_falls_through_to_device_code()
    {
        var auth = new SharedSecretAuthenticator(SharedSecret, NewTimeAligner());

        Assert.False(await auth.AcceptDeviceConfirmationAsync());
    }

    private static TimeAligner NewTimeAligner(long serverTime = 1_700_000_000L, long localTime = 1_700_000_000L)
    {
        var http = new HttpClient(new StubHandler($$$"""{"response":{"server_time":"{{{serverTime}}}"}}"""));
        return new TimeAligner(http, new FixedClock(localTime));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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
