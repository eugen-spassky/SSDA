using SteamKit2.Authentication;

namespace SSDA.Core.Auth;

/// <summary>
/// <see cref="IAuthenticator"/> for accounts that have **no** existing Steam Guard
/// authenticator yet — i.e. the link / first-time-add flow. If Steam unexpectedly
/// requests a 2FA code, the call surfaces a <see cref="SteamLoginException"/> instead
/// of silently hanging the polling loop.
/// </summary>
public sealed class EmptyAuthenticator : IAuthenticator
{
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        => throw new SteamLoginException(
            "Steam unexpectedly required an existing device code, but this account is being " +
            "linked for the first time. The account may already have an authenticator attached.");

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        => throw new SteamLoginException(
            "This account uses email-based Steam Guard. Disable email Guard before linking " +
            "the mobile authenticator from SSDA.");

    public Task<bool> AcceptDeviceConfirmationAsync() => Task.FromResult(false);
}
