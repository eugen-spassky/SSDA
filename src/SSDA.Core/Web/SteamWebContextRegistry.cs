using System.Net.Http;
using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.Core.Web;

/// <summary>
/// Keeps one cookie-bearing <see cref="HttpClient"/> per account so the
/// mobile-conf endpoints share auth state. The registry owns its clients and
/// disposes them on shutdown.
/// </summary>
public sealed class SteamWebContextRegistry : IDisposable
{
    private readonly TimeAligner _time;
    private readonly HttpClient _bareClient;
    private readonly Dictionary<ulong, AccountClient> _clients = new();
    private readonly object _gate = new();
    private bool _disposed;

    public SteamWebContextRegistry()
    {
        _bareClient = new HttpClient();
        _time = new TimeAligner(_bareClient);
    }

    /// <summary>Exposed so other services (e.g. login) can share the cached offset.</summary>
    public TimeAligner TimeAligner => _time;

    /// <summary>
    /// Returns a <see cref="SteamMobileConfClient"/> bound to <paramref name="account"/>.
    /// Throws if the account has no usable session.
    /// </summary>
    public SteamMobileConfClient GetMobileConfClient(SteamGuardAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var session = account.Session
            ?? throw new InvalidOperationException("Account has no Session.");
        if (string.IsNullOrEmpty(session.AccessToken))
            throw new InvalidOperationException("Account session has no AccessToken.");
        if (session.SteamID == 0)
            throw new InvalidOperationException("Account session has no SteamID.");

        HttpClient http;
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SteamWebContextRegistry));

            if (!_clients.TryGetValue(session.SteamID, out var entry)
                || entry.Token != session.AccessToken)
            {
                entry?.Http.Dispose();
                entry = CreateClient(session);
                _clients[session.SteamID] = entry;
            }
            http = entry.Http;
        }

        return new SteamMobileConfClient(http, _time);
    }

    private static AccountClient CreateClient(SessionData session)
    {
        var web = new SteamWebSession(session.SteamID, session.AccessToken!);
        var http = new HttpClient(web.CreateHandler());
        return new AccountClient(http, session.AccessToken!);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var c in _clients.Values) c.Http.Dispose();
            _clients.Clear();
        }
        _bareClient.Dispose();
    }

    private sealed record AccountClient(HttpClient Http, string Token);
}
