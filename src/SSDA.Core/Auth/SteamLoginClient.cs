using SteamKit2;
using SteamKit2.Authentication;

namespace SSDA.Core.Auth;

/// <summary>
/// Drives a Steam sign-in via SteamKit2 3.0's <see cref="SteamAuthentication"/> flow.
/// Connects, runs <c>BeginAuthSessionViaCredentialsAsync</c>, polls for the
/// <see cref="AuthPollResult"/>, then disconnects. Each call owns its own
/// <see cref="SteamClient"/> — instances are short-lived, do not share them.
/// </summary>
public sealed class SteamLoginClient
{
    private readonly Func<SteamClient> _clientFactory;
    private readonly TimeSpan _connectTimeout;

    /// <summary>Initialises a client backed by a default <see cref="SteamClient"/>.</summary>
    public SteamLoginClient() : this(() => new SteamClient(), TimeSpan.FromSeconds(30)) { }

    internal SteamLoginClient(Func<SteamClient> clientFactory, TimeSpan connectTimeout)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _connectTimeout = connectTimeout;
    }

    /// <summary>
    /// Signs in with username + password, deferring 2-factor auth to the supplied
    /// <paramref name="authenticator"/>. On success returns the freshly-issued tokens.
    /// </summary>
    public async Task<SteamLoginResult> LoginAsync(
        string username,
        string password,
        IAuthenticator authenticator,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required.", nameof(password));
        ArgumentNullException.ThrowIfNull(authenticator);

        var client = _clientFactory();
        var manager = new CallbackManager(client);

        // Pump callbacks for the entire lifetime of this login. Both the connect step
        // and BeginAuthSessionViaCredentialsAsync depend on callbacks being delivered.
        using var pumpCts = new CancellationTokenSource();
        var pump = Task.Run(() =>
        {
            while (!pumpCts.IsCancellationRequested)
                manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
        }, CancellationToken.None);

        try
        {
            await ConnectAsync(client, manager, ct).ConfigureAwait(false);

            CredentialsAuthSession session;
            try
            {
                session = await client.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = username,
                        Password = password,
                        IsPersistentSession = false,
                        Authenticator = authenticator,
                        DeviceFriendlyName = "SSDA",
                        PlatformType = SteamKit2.Internal.EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                        ClientOSType = EOSType.AndroidUnknown,
                    }).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                throw new SteamLoginException(
                    "Steam rejected the credentials. Check the username and password.", ex);
            }

            AuthPollResult poll;
            try
            {
                poll = await session.PollingWaitForResultAsync(ct).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                throw new SteamLoginException("Steam aborted the auth session.", ex);
            }

            return new SteamLoginResult(
                SteamID: session.SteamID.ConvertToUInt64(),
                AccountName: poll.AccountName ?? string.Empty,
                AccessToken: poll.AccessToken ?? string.Empty,
                RefreshToken: poll.RefreshToken ?? string.Empty,
                NewGuardData: poll.NewGuardData);
        }
        finally
        {
            try { client.Disconnect(); } catch { /* best-effort */ }
            pumpCts.Cancel();
            try { await pump.ConfigureAwait(false); } catch { /* swallow */ }
        }
    }

    private async Task ConnectAsync(SteamClient client, CallbackManager manager, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<SteamClient.ConnectedCallback>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var sub = manager.Subscribe<SteamClient.ConnectedCallback>(cb => tcs.TrySetResult(cb));
        using var disc = manager.Subscribe<SteamClient.DisconnectedCallback>(cb =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetException(new SteamLoginException(
                    cb.UserInitiated
                        ? "Connection cancelled before Steam responded."
                        : "Could not connect to Steam — network error or Steam is down."));
        });

        client.Connect();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(_connectTimeout);
        using var reg = linked.Token.Register(() =>
            tcs.TrySetException(ct.IsCancellationRequested
                ? new OperationCanceledException(ct)
                : new SteamLoginException("Timed out waiting for Steam to accept the connection.")));

        await tcs.Task.ConfigureAwait(false);
    }
}
