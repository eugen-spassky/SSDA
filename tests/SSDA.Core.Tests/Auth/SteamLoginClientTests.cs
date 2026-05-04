using SSDA.Core.Auth;
using SteamKit2.Authentication;
using Xunit;

namespace SSDA.Core.Tests.Auth;

public class SteamLoginClientTests
{
    [Fact]
    public async Task LoginAsync_throws_when_username_is_empty()
    {
        var client = new SteamLoginClient();
        var auth = new StubAuthenticator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.LoginAsync("", "password", auth));
    }

    [Fact]
    public async Task LoginAsync_throws_when_password_is_empty()
    {
        var client = new SteamLoginClient();
        var auth = new StubAuthenticator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.LoginAsync("username", "", auth));
    }

    [Fact]
    public async Task LoginAsync_throws_when_authenticator_is_null()
    {
        var client = new SteamLoginClient();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.LoginAsync("username", "password", null!));
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_throws_when_steamId_is_zero()
    {
        var client = new SteamLoginClient();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RefreshAccessTokenAsync(0, "refresh"));
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_throws_when_refreshToken_is_empty()
    {
        var client = new SteamLoginClient();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RefreshAccessTokenAsync(76561198000000000UL, ""));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RefreshAccessTokenAsync(76561198000000000UL, null!));
    }

    [Fact]
    public void SteamLoginResult_carries_all_fields_through_record_equality()
    {
        var a = new SteamLoginResult(
            SteamID: 76561198000000000,
            AccountName: "alice",
            AccessToken: "access",
            RefreshToken: "refresh",
            NewGuardData: "guard");

        var b = new SteamLoginResult(
            SteamID: 76561198000000000,
            AccountName: "alice",
            AccessToken: "access",
            RefreshToken: "refresh",
            NewGuardData: "guard");

        Assert.Equal(a, b);
        Assert.Equal(76561198000000000UL, a.SteamID);
        Assert.Equal("alice", a.AccountName);
        Assert.Equal("access", a.AccessToken);
        Assert.Equal("refresh", a.RefreshToken);
        Assert.Equal("guard", a.NewGuardData);
    }

    private sealed class StubAuthenticator : IAuthenticator
    {
        public Task<string> GetDeviceCodeAsync(bool _) => Task.FromResult("00000");
        public Task<string> GetEmailCodeAsync(string _, bool __) => Task.FromResult("00000");
        public Task<bool> AcceptDeviceConfirmationAsync() => Task.FromResult(false);
    }
}
