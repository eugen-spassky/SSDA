using System.Net;
using System.Security.Cryptography;
using System.Text;
using SSDA.Core.Web;

namespace SSDA.Core.Tests.Web;

public sealed class MobileConfTagSignerTests
{
    private const string IdentitySecretBase64 =
        "VHIKCgQOFRYWGRoaHB0eHyAhIiMkJSYnKCkqKywtLi8wMQ=="; // arbitrary 38-byte fixture

    [Theory]
    [InlineData(1700000000L, "list")]
    [InlineData(1234567890L, "conf")]
    [InlineData(1L, "allow")]
    [InlineData(9999999999L, "reject")]
    [InlineData(123L, "multi")]
    public void Sign_matches_reference_implementation(long time, string tag)
    {
        var actual = MobileConfTagSigner.Sign(IdentitySecretBase64, time, tag);
        var expected = ReferenceSign(IdentitySecretBase64, time, tag);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Sign_truncates_long_tag_to_32_bytes()
    {
        var longTag = new string('x', 64);
        var actual = MobileConfTagSigner.Sign(IdentitySecretBase64, 1L, longTag);
        var expected = ReferenceSign(IdentitySecretBase64, 1L, longTag);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Sign_url_encodes_base64_padding()
    {
        // base64 outputs include '+', '/', '=' which must be percent-encoded.
        var output = MobileConfTagSigner.Sign(IdentitySecretBase64, 1700000000L, "list");
        Assert.DoesNotContain('+', output);
        Assert.DoesNotContain('/', output);
        Assert.DoesNotContain('=', output);
    }

    /// <summary>
    /// Verbatim port of <c>GenerateConfirmationHashForTime</c> from the original
    /// jessecar96/SteamDesktopAuthenticator. Used to lock the implementation byte-for-byte.
    /// </summary>
    private static string ReferenceSign(string identitySecret, long time, string tag)
    {
        var key = Convert.FromBase64String(identitySecret);
        var n2 = 8;
        if (tag.Length > 32) n2 = 8 + 32;
        else n2 = 8 + tag.Length;
        var array = new byte[n2];
        var n3 = 8;
        while (true)
        {
            var n4 = n3 - 1;
            if (n3 <= 0) break;
            array[n4] = (byte)time;
            time >>= 8;
            n3 = n4;
        }
        Array.Copy(Encoding.UTF8.GetBytes(tag), 0, array, 8, n2 - 8);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(array);
        var encoded = Convert.ToBase64String(hash, Base64FormattingOptions.None);
        return WebUtility.UrlEncode(encoded)!;
    }
}
