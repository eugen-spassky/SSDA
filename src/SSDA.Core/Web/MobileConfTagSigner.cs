using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SSDA.Core.Web;

/// <summary>
/// Generates the <c>k=</c> query parameter that authenticates a request against
/// <c>steamcommunity.com/mobileconf/*</c>. The value is a URL-encoded HMAC-SHA1 of
/// <c>(big-endian time bytes ‖ tag UTF-8 bytes)</c> keyed by the Base64-decoded
/// <c>identity_secret</c>.
/// </summary>
public static class MobileConfTagSigner
{
    private const int MaxTagLength = 32;

    /// <summary>
    /// Computes the URL-encoded base64 HMAC tag.
    /// </summary>
    /// <param name="identitySecretBase64">Account's <c>identity_secret</c> from the maFile.</param>
    /// <param name="time">Steam-aligned unix time in seconds.</param>
    /// <param name="tag">Operation tag (e.g. <c>list</c>, <c>conf</c>, <c>allow</c>, <c>reject</c>, <c>multi</c>).</param>
    public static string Sign(string identitySecretBase64, long time, string tag)
    {
        ArgumentNullException.ThrowIfNull(identitySecretBase64);
        ArgumentNullException.ThrowIfNull(tag);

        var key = Convert.FromBase64String(identitySecretBase64);
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        if (tagBytes.Length > MaxTagLength)
            tagBytes = tagBytes.AsSpan(0, MaxTagLength).ToArray();

        Span<byte> buffer = stackalloc byte[8 + MaxTagLength];
        WriteBigEndianTime(buffer, time);
        tagBytes.CopyTo(buffer[8..]);
        var payload = buffer[..(8 + tagBytes.Length)];

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, payload, hash);

        var b64 = Convert.ToBase64String(hash);
        return WebUtility.UrlEncode(b64) ?? string.Empty;
    }

    private static void WriteBigEndianTime(Span<byte> destination, long time)
    {
        for (var i = 7; i >= 0; i--)
        {
            destination[i] = (byte)time;
            time >>= 8;
        }
    }
}
