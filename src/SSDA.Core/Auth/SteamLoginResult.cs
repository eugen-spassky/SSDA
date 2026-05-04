namespace SSDA.Core.Auth;

/// <summary>
/// The successful outcome of a <see cref="SteamLoginClient"/> sign-in. Maps directly to
/// <c>SteamKit2.Authentication.AuthPollResult</c> with the resolved <see cref="SteamID"/>
/// from the credentials session attached.
/// </summary>
public sealed record SteamLoginResult(
    ulong SteamID,
    string AccountName,
    string AccessToken,
    string RefreshToken,
    string? NewGuardData);
