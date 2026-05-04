using SSDA.Core.Crypto;
using SSDA.Core.Services;
using SteamKit2.Authentication;

namespace SSDA.Core.Auth;

/// <summary>
/// SteamKit <see cref="IAuthenticator"/> that auto-supplies the device code from a known
/// <c>shared_secret</c> instead of prompting the user. Used when re-establishing a session
/// for an account whose authenticator we already own.
/// </summary>
public sealed class SharedSecretAuthenticator : IAuthenticator
{
    private readonly string _sharedSecretBase64;
    private readonly TimeAligner _time;

    /// <summary>
    /// </summary>
    /// <param name="sharedSecretBase64">The account's <c>shared_secret</c> (raw base64).</param>
    /// <param name="time">Steam-aligned clock for choosing the 30-second window.</param>
    public SharedSecretAuthenticator(string sharedSecretBase64, TimeAligner time)
    {
        if (string.IsNullOrEmpty(sharedSecretBase64))
            throw new ArgumentException("shared_secret is required.", nameof(sharedSecretBase64));
        _sharedSecretBase64 = sharedSecretBase64;
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc/>
    public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        if (previousCodeWasIncorrect)
            throw new SteamLoginException(
                "Steam rejected the generated device code. The account's shared_secret may be wrong, " +
                "or the system clock is too far off.");

        var time = await _time.GetSteamTimeAsync().ConfigureAwait(false);
        return SteamTotpGenerator.Generate(_sharedSecretBase64, time);
    }

    /// <inheritdoc/>
    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        => throw new SteamLoginException(
            "This account uses email-based Steam Guard. Email Guard is not supported for re-login " +
            "from SSDA — disable email Guard or finish migration to mobile authenticator first.");

    /// <inheritdoc/>
    public Task<bool> AcceptDeviceConfirmationAsync()
        // We *are* the device. Returning false forces SteamKit to fall through to
        // GetDeviceCodeAsync, which generates a code from the shared_secret instead of
        // waiting for a push notification that would never come.
        => Task.FromResult(false);
}
