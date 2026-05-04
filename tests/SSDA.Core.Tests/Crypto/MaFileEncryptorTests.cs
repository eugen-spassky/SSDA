using System.Security.Cryptography;
using System.Text;
using SSDA.Core.Crypto;

namespace SSDA.Core.Tests.Crypto;

public sealed class MaFileEncryptorTests
{
    [Fact]
    public void Generated_salt_is_eight_random_bytes_in_base64()
    {
        var salt = MaFileEncryptor.GenerateSaltBase64();
        var bytes = Convert.FromBase64String(salt);
        Assert.Equal(MaFileEncryptor.SaltLengthBytes, bytes.Length);
    }

    [Fact]
    public void Generated_iv_is_sixteen_random_bytes_in_base64()
    {
        var iv = MaFileEncryptor.GenerateIvBase64();
        var bytes = Convert.FromBase64String(iv);
        Assert.Equal(MaFileEncryptor.IvLengthBytes, bytes.Length);
    }

    [Fact]
    public void DeriveKey_yields_thirty_two_bytes_using_pbkdf2_sha1_50000_iterations()
    {
        var saltBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var salt = Convert.ToBase64String(saltBytes);

        var key = MaFileEncryptor.DeriveKey("super-secret-passkey", salt);

        var expected = Rfc2898DeriveBytes.Pbkdf2(
            password: "super-secret-passkey",
            salt: saltBytes,
            iterations: MaFileEncryptor.Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA1,
            outputLength: MaFileEncryptor.KeySizeBytes);

        Assert.Equal(MaFileEncryptor.KeySizeBytes, key.Length);
        Assert.Equal(expected, key);
    }

    [Fact]
    public void Encrypt_then_decrypt_roundtrip_returns_the_original_plaintext()
    {
        var salt = MaFileEncryptor.GenerateSaltBase64();
        var iv = MaFileEncryptor.GenerateIvBase64();
        const string plaintext = "{\"shared_secret\":\"abc\",\"identity_secret\":\"def\"}";

        var cipher = MaFileEncryptor.Encrypt("hunter2", salt, iv, plaintext);
        var decrypted = MaFileEncryptor.TryDecrypt("hunter2", salt, iv, cipher);

        Assert.NotEqual(plaintext, cipher);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TryDecrypt_returns_null_for_a_wrong_passkey()
    {
        var salt = MaFileEncryptor.GenerateSaltBase64();
        var iv = MaFileEncryptor.GenerateIvBase64();
        var cipher = MaFileEncryptor.Encrypt("right-passkey", salt, iv, "secrets");

        var result = MaFileEncryptor.TryDecrypt("wrong-passkey", salt, iv, cipher);

        Assert.Null(result);
    }

    [Fact]
    public void Encrypt_throws_for_empty_passkey()
    {
        Assert.Throws<ArgumentException>(
            () => MaFileEncryptor.Encrypt("", MaFileEncryptor.GenerateSaltBase64(), MaFileEncryptor.GenerateIvBase64(), "x"));
    }

    [Fact]
    public void Encrypt_throws_for_iv_of_wrong_length()
    {
        var salt = MaFileEncryptor.GenerateSaltBase64();
        var shortIv = Convert.ToBase64String(new byte[8]);

        Assert.Throws<ArgumentException>(
            () => MaFileEncryptor.Encrypt("p", salt, shortIv, "x"));
    }

    [Fact]
    public void Two_encryptions_of_the_same_plaintext_with_different_iv_produce_different_ciphertexts()
    {
        const string plaintext = "the-same-input";
        var salt = MaFileEncryptor.GenerateSaltBase64();

        var first = MaFileEncryptor.Encrypt("p", salt, MaFileEncryptor.GenerateIvBase64(), plaintext);
        var second = MaFileEncryptor.Encrypt("p", salt, MaFileEncryptor.GenerateIvBase64(), plaintext);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// The original SDA writes the AES-CBC ciphertext directly as base64 with PKCS7
    /// padding; this test reproduces a ciphertext using <c>System.Security.Cryptography</c>
    /// primitives manually and confirms <see cref="MaFileEncryptor.TryDecrypt"/> can read it.
    /// </summary>
    [Fact]
    public void Decrypts_a_payload_produced_with_raw_AesCryptoServiceProvider_style_inputs()
    {
        const string passkey = "interop-passkey";
        const string plaintext = "interop-payload";
        var saltBytes = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
        var ivBytes = Enumerable.Range(0, 16).Select(i => (byte)(i * 11)).ToArray();
        var salt = Convert.ToBase64String(saltBytes);
        var iv = Convert.ToBase64String(ivBytes);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            password: passkey,
            salt: saltBytes,
            iterations: MaFileEncryptor.Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA1,
            outputLength: MaFileEncryptor.KeySizeBytes);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = ivBytes;
        using var enc = aes.CreateEncryptor();
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var cipher = Convert.ToBase64String(enc.TransformFinalBlock(pt, 0, pt.Length));

        var decrypted = MaFileEncryptor.TryDecrypt(passkey, salt, iv, cipher);

        Assert.Equal(plaintext, decrypted);
    }
}
