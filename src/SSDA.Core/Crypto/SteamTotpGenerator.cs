using System.Security.Cryptography;
using System.Text;

namespace SSDA.Core.Crypto;

/// <summary>
/// Generates Steam Guard TOTP codes. Steam uses a 30-second time step with HMAC-SHA1
/// over the big-endian 8-byte step counter, then re-encodes the dynamic-truncated
/// integer in a custom 26-symbol alphabet (digits + a subset of uppercase letters)
/// as a 5-character code.
/// </summary>
public static class SteamTotpGenerator
{
    /// <summary>The 26 characters used in Steam Guard codes, in order.</summary>
    public const string Alphabet = "23456789BCDFGHJKMNPQRTVWXY";

    private static readonly byte[] AlphabetBytes = Encoding.ASCII.GetBytes(Alphabet);

    /// <summary>The Steam TOTP step length in seconds.</summary>
    public const int StepSeconds = 30;

    /// <summary>The number of characters in a generated code.</summary>
    public const int CodeLength = 5;

    /// <summary>
    /// Generates the 5-character Steam Guard code for the supplied unix timestamp
    /// (seconds) and base64-encoded shared secret.
    /// </summary>
    public static string Generate(string sharedSecretBase64, long unixTimeSeconds)
    {
        if (string.IsNullOrEmpty(sharedSecretBase64))
            throw new ArgumentException("Shared secret is empty.", nameof(sharedSecretBase64));
        if (unixTimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unixTimeSeconds));

        var secret = Convert.FromBase64String(sharedSecretBase64);
        var step = unixTimeSeconds / StepSeconds;

        Span<byte> stepBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            stepBytes[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        using (var hmac = new HMACSHA1(secret))
        {
            if (!hmac.TryComputeHash(stepBytes, hash, out _))
                throw new CryptographicException("HMAC-SHA1 failed.");
        }

        var offset = hash[19] & 0x0F;
        var truncated =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        Span<byte> code = stackalloc byte[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            code[i] = AlphabetBytes[truncated % AlphabetBytes.Length];
            truncated /= AlphabetBytes.Length;
        }

        return Encoding.ASCII.GetString(code);
    }

    /// <summary>
    /// Returns how many seconds remain in the current 30-second window for the
    /// supplied unix timestamp.
    /// </summary>
    public static int SecondsRemainingInWindow(long unixTimeSeconds)
    {
        if (unixTimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unixTimeSeconds));
        return StepSeconds - (int)(unixTimeSeconds % StepSeconds);
    }
}
