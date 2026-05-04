using System.Security.Cryptography;
using System.Text;
using SSDA.Core.Crypto;

namespace SSDA.Core.Tests.Crypto;

public sealed class SteamTotpGeneratorTests
{
    /// <summary>
    /// Direct port of <c>SteamGuardAccount.GenerateSteamGuardCodeForTime</c> from
    /// <c>jessecar96/SteamDesktopAuthenticator</c>. Used as the ground truth — any
    /// divergence between this and <see cref="SteamTotpGenerator.Generate"/> would mean
    /// existing maFiles produce different codes between SSDA and the original tool, which
    /// would block every legitimate Steam confirmation.
    /// </summary>
    private static string ReferenceGenerate(byte[] secret, long time)
    {
        var timeArray = new byte[8];
        time /= 30L;
        for (var i = 8; i > 0; i--)
        {
            timeArray[i - 1] = (byte)time;
            time >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeArray);
        var alphabet = new byte[]
        {
            50, 51, 52, 53, 54, 55, 56, 57, 66, 67, 68, 70, 71, 72,
            74, 75, 77, 78, 80, 81, 82, 84, 86, 87, 88, 89,
        };

        var b = (byte)(hash[19] & 0xF);
        var code = (hash[b] & 0x7F) << 24
            | (hash[b + 1] & 0xFF) << 16
            | (hash[b + 2] & 0xFF) << 8
            | (hash[b + 3] & 0xFF);

        var output = new byte[5];
        for (var i = 0; i < 5; i++)
        {
            output[i] = alphabet[code % alphabet.Length];
            code /= alphabet.Length;
        }

        return Encoding.UTF8.GetString(output);
    }

    private static readonly byte[] FixedSecret = Enumerable.Range(1, 20).Select(b => (byte)b).ToArray();
    private static readonly string FixedSecretBase64 = Convert.ToBase64String(FixedSecret);

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(29L)]
    [InlineData(30L)]
    [InlineData(31L)]
    [InlineData(1_700_000_000L)]
    [InlineData(1_734_555_999L)]
    [InlineData(2_000_000_000L)]
    public void Generate_matches_reference_implementation(long time)
    {
        var expected = ReferenceGenerate(FixedSecret, time);
        var actual = SteamTotpGenerator.Generate(FixedSecretBase64, time);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Generate_produces_five_character_codes_using_only_the_steam_alphabet()
    {
        var rnd = new Random(42);
        var secret = new byte[20];
        for (var iteration = 0; iteration < 256; iteration++)
        {
            rnd.NextBytes(secret);
            var time = (long)rnd.Next(int.MaxValue);
            var code = SteamTotpGenerator.Generate(Convert.ToBase64String(secret), time);

            Assert.Equal(SteamTotpGenerator.CodeLength, code.Length);
            foreach (var ch in code)
                Assert.Contains(ch, SteamTotpGenerator.Alphabet);
        }
    }

    [Fact]
    public void Generate_is_deterministic_for_the_same_inputs()
    {
        var first = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_123L);
        var second = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_123L);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_returns_the_same_code_within_a_thirty_second_window()
    {
        var a = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_100L);
        var b = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_119L);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Generate_rolls_over_at_thirty_second_boundaries()
    {
        // Codes at the bottom and top of the same 30s window must match; codes in the
        // next window will *almost always* differ. Pick a secret where the next-window
        // code differs from the current to keep the assertion deterministic.
        var inWindow = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_120L);
        var nextWindow = SteamTotpGenerator.Generate(FixedSecretBase64, 1_700_000_150L);
        Assert.NotEqual(inWindow, nextWindow);
    }

    [Fact]
    public void Generate_throws_on_empty_secret()
    {
        Assert.Throws<ArgumentException>(() => SteamTotpGenerator.Generate("", 0));
    }

    [Fact]
    public void Generate_throws_on_negative_time()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SteamTotpGenerator.Generate(FixedSecretBase64, -1));
    }

    [Theory]
    [InlineData(0L, 30)]
    [InlineData(1L, 29)]
    [InlineData(29L, 1)]
    [InlineData(30L, 30)]
    [InlineData(45L, 15)]
    [InlineData(59L, 1)]
    [InlineData(60L, 30)]
    public void SecondsRemaining_returns_seconds_left_in_window(long time, int expected)
    {
        Assert.Equal(expected, SteamTotpGenerator.SecondsRemainingInWindow(time));
    }

    [Fact]
    public void Alphabet_matches_the_steam_definition()
    {
        Assert.Equal(26, SteamTotpGenerator.Alphabet.Length);
        Assert.Equal("23456789BCDFGHJKMNPQRTVWXY", SteamTotpGenerator.Alphabet);
        Assert.All(SteamTotpGenerator.Alphabet, c =>
        {
            Assert.True(char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c));
        });
    }
}
