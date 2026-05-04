using System.Security.Cryptography;
using System.Text;

namespace SSDA.Core.Web;

/// <summary>
/// Generates the <c>k=</c> parameter that authenticates a request against
/// <c>steamcommunity.com/mobileconf/*</c>. The value is the raw base64 HMAC-SHA1 of
/// <c>(big-endian time bytes ‖ tag UTF-8 bytes)</c> keyed by the Base64-decoded
/// <c>identity_secret</c>. Callers that splice the value directly into a URL query
/// must URL-encode it; callers that hand it to <c>FormUrlEncodedContent</c> must not
/// (it would double-encode the base64 padding).
/// </summary>
public static class MobileConfTagSigner
{
    private const int MaxTagLength = 32;

    /// <summary>
    /// Computes the raw base64-encoded HMAC tag (no URL-encoding).
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

        return Convert.ToBase64String(hash);
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
