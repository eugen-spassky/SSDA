using System.Net;
using SSDA.Core.Web;

namespace SSDA.Core.Tests.Web;

public sealed class SteamWebSessionTests
{
    [Fact]
    public void Constructor_populates_required_cookies()
    {
        var session = new SteamWebSession(76561198123456789UL, "ACCESS_TOKEN");
        var cookies = session.Cookies.GetCookies(new Uri("https://steamcommunity.com/"));

        Assert.Equal(
            "76561198123456789||ACCESS_TOKEN",
            cookies["steamLoginSecure"]?.Value);
        Assert.Equal("android", cookies["mobileClient"]?.Value);
        Assert.Equal("777777 3.0.0", cookies["mobileClientVersion"]?.Value);
        Assert.Equal("english", cookies["Steam_Language"]?.Value);
        Assert.False(string.IsNullOrEmpty(cookies["sessionid"]?.Value));
    }

    [Fact]
    public void Constructor_url_encodes_special_chars_in_access_token()
    {
        var session = new SteamWebSession(1UL, "abc def+/=");
        var cookies = session.Cookies.GetCookies(new Uri("https://steamcommunity.com/"));

        Assert.Contains("abc+def", cookies["steamLoginSecure"]!.Value);
        Assert.DoesNotContain(' ', cookies["steamLoginSecure"]!.Value);
    }

    [Fact]
    public void Constructor_rejects_zero_steamid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SteamWebSession(0UL, "tok"));
    }

    [Fact]
    public void Constructor_rejects_empty_access_token()
    {
        Assert.Throws<ArgumentException>(
            () => new SteamWebSession(1UL, string.Empty));
    }

    [Fact]
    public void Constructor_uses_supplied_session_id()
    {
        var session = new SteamWebSession(1UL, "tok", sessionId: "deadbeef");
        Assert.Equal("deadbeef", session.SessionId);
    }
}
