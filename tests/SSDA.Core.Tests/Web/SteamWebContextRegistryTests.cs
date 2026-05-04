using SSDA.Core.Models;
using SSDA.Core.Web;

namespace SSDA.Core.Tests.Web;

public sealed class SteamWebContextRegistryTests
{
    [Fact]
    public void GetMobileConfClient_caches_per_steamid()
    {
        using var reg = new SteamWebContextRegistry();
        var account = MakeAccount(steamId: 1, token: "tok-a");

        var a = reg.GetMobileConfClient(account);
        var b = reg.GetMobileConfClient(account);

        Assert.NotNull(a);
        Assert.NotNull(b);
    }

    [Fact]
    public void GetMobileConfClient_throws_when_session_missing()
    {
        using var reg = new SteamWebContextRegistry();
        var account = new SteamGuardAccount { AccountName = "x" };
        Assert.Throws<InvalidOperationException>(() => reg.GetMobileConfClient(account));
    }

    [Fact]
    public void GetMobileConfClient_throws_when_steamid_zero()
    {
        using var reg = new SteamWebContextRegistry();
        var account = MakeAccount(steamId: 0, token: "tok");
        Assert.Throws<InvalidOperationException>(() => reg.GetMobileConfClient(account));
    }

    [Fact]
    public void GetMobileConfClient_throws_when_token_empty()
    {
        using var reg = new SteamWebContextRegistry();
        var account = MakeAccount(steamId: 1, token: string.Empty);
        Assert.Throws<InvalidOperationException>(() => reg.GetMobileConfClient(account));
    }

    [Fact]
    public void GetMobileConfClient_after_dispose_throws()
    {
        var reg = new SteamWebContextRegistry();
        reg.Dispose();
        Assert.Throws<ObjectDisposedException>(
            () => reg.GetMobileConfClient(MakeAccount(steamId: 1, token: "tok")));
    }

    [Fact]
    public void GetMobileConfClient_rebuilds_when_token_changes()
    {
        using var reg = new SteamWebContextRegistry();
        var first = MakeAccount(steamId: 42, token: "old");
        var second = MakeAccount(steamId: 42, token: "new");

        reg.GetMobileConfClient(first);
        var client = reg.GetMobileConfClient(second);

        Assert.NotNull(client);
    }

    private static SteamGuardAccount MakeAccount(ulong steamId, string token) => new()
    {
        AccountName = "fixture",
        IdentitySecret = "VHIKCgQOFRYWGRoaHB0eHyAhIiMkJSYnKCkqKywtLi8wMQ==",
        DeviceID = "device-fixture",
        Session = new SessionData { SteamID = steamId, AccessToken = token },
    };
}
