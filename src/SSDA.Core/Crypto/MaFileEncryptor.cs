using System.Security.Cryptography;
using System.Text;

namespace SSDA.Core.Crypto;

/// <summary>
/// Encrypts and decrypts the body of a <c>.maFile</c> using the same scheme as the
/// reference <c>jessecar96/SteamDesktopAuthenticator</c> implementation:
/// PBKDF2-HMAC-SHA1 (50&#160;000 iterations, 32-byte key) → AES-256-CBC, PKCS7 padding.
/// The key is derived from the user passkey and an 8-byte salt. The IV is 16 bytes.
/// Both salt and IV are stored in <c>manifest.json</c>; the file body is the raw base64
/// of the AES ciphertext.
/// </summary>
public static class MaFileEncryptor
{
    /// <summary>Iteration count baked into the original SDA. Do not change.</summary>
    public const int Pbkdf2Iterations = 50_000;

    /// <summary>Salt length stored alongside each maFile entry.</summary>
    public const int SaltLengthBytes = 8;

    /// <summary>AES-256 key size in bytes.</summary>
    public const int KeySizeBytes = 32;

    /// <summary>AES block / IV size in bytes.</summary>
    public const int IvLengthBytes = 16;

    /// <summary>Returns a fresh, base64-encoded 8-byte cryptographically-strong salt.</summary>
    public static string GenerateSaltBase64()
    {
        var salt = new byte[SaltLengthBytes];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
    }

    /// <summary>Returns a fresh, base64-encoded 16-byte cryptographically-strong IV.</summary>
    public static string GenerateIvBase64()
    {
        var iv = new byte[IvLengthBytes];
        RandomNumberGenerator.Fill(iv);
        return Convert.ToBase64String(iv);
    }

    /// <summary>
    /// Derives the AES-256 key for a passkey + salt pair using PBKDF2-SHA1 with
    /// <see cref="Pbkdf2Iterations"/> rounds.
    /// </summary>
    public static byte[] DeriveKey(string passkey, string saltBase64)
    {
        if (string.IsNullOrEmpty(passkey))
            throw new ArgumentException("Passkey is empty.", nameof(passkey));
        if (string.IsNullOrEmpty(saltBase64))
            throw new ArgumentException("Salt is empty.", nameof(saltBase64));

        var salt = Convert.FromBase64String(saltBase64);
        return Rfc2898DeriveBytes.Pbkdf2(
            password: passkey,
            salt: salt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA1,
            outputLength: KeySizeBytes);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using a passkey, salt, and IV. Returns the
    /// AES ciphertext as a base64 string suitable for writing directly to a <c>.maFile</c>.
    /// </summary>
    public static string Encrypt(string passkey, string saltBase64, string ivBase64, string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var key = DeriveKey(passkey, saltBase64);
        var iv = Convert.FromBase64String(ivBase64);
        if (iv.Length != IvLengthBytes)
            throw new ArgumentException($"IV must decode to {IvLengthBytes} bytes.", nameof(ivBase64));

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(cipher);
    }

    /// <summary>
    /// Decrypts a base64-encoded AES ciphertext produced by <see cref="Encrypt"/>.
    /// Returns <c>null</c> if the passkey is wrong (which manifests as a padding /
    /// decryption error). Throws if the inputs are structurally invalid.
    /// </summary>
    public static string? TryDecrypt(string passkey, string saltBase64, string ivBase64, string cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64))
            throw new ArgumentException("Ciphertext is empty.", nameof(cipherBase64));

        var key = DeriveKey(passkey, saltBase64);
        var iv = Convert.FromBase64String(ivBase64);
        if (iv.Length != IvLengthBytes)
            throw new ArgumentException($"IV must decode to {IvLengthBytes} bytes.", nameof(ivBase64));

        var cipher = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        try
        {
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
